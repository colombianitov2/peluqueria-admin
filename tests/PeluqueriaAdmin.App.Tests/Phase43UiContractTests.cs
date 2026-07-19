using System.IO;
using PeluqueriaAdmin.App.ViewModels;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase43UiContractTests
{
    [Theory]
    [InlineData("queso rancio", "QUESO")]
    [InlineData("pepino salado", "PÉPINO")]
    [InlineData("Cortesía", "cortesia")]
    public void SalesProductSearch_IgnoresCaseAndAccents(string product, string search)
    {
        Assert.True(SalesViewModel.MatchesProductSearch(product, search));
    }

    [Fact]
    public void SalesProductSearch_ShowsBothProductsFiltersQuesoAndRestoresBothWhenCleared()
    {
        string[] products = ["queso rancio", "pepino salado"];

        Assert.Equal(products, products.Where(item => SalesViewModel.MatchesProductSearch(item, string.Empty)));
        Assert.Equal(["queso rancio"], products.Where(item => SalesViewModel.MatchesProductSearch(item, "ques")));
        Assert.Equal(products, products.Where(item => SalesViewModel.MatchesProductSearch(item, string.Empty)));
    }

    [Fact]
    public void SalesAndInventory_UseDedicatedViewsWithoutSeparateSearchFieldOrMixedRows()
    {
        string sales = Read("src", "PeluqueriaAdmin.App", "Views", "SalesView.xaml");
        string inventory = Read("src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");

        Assert.DoesNotContain("Buscar por nombre", sales, StringComparison.Ordinal);
        Assert.Contains("Precio de venta por unidad o paquete", sales, StringComparison.Ordinal);
        Assert.Contains("No se encontraron productos", sales, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding CurrentInventory}\"", inventory, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding MovementHistory}\"", inventory, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding RestockPlans}\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Header=\"Cantidad que entra\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Header=\"Cantidad que sale\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Header=\"Costo unitario\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Header=\"Valor total\"", inventory, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(70_000_000d, "70.000.000")]
    [InlineData(80_000_000_000d, "80.000.000.000")]
    public void ChartAxisValues_UseFullSpanishThousandsWithoutScientificNotation(double value, string expected)
    {
        string formatted = AdministrationViewModel.FormatChartAxisValue(value);

        Assert.DoesNotContain("E", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expected, formatted);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. parts]));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PeluqueriaAdmin.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
