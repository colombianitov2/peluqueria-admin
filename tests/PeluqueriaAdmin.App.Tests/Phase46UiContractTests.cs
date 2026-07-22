using System.IO;
using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase46UiContractTests
{
    [Theory]
    [InlineData(1, 35)]
    [InlineData(2, 70)]
    [InlineData(3, 105)]
    public void SalesPreview_UsesExactMinorUnits(decimal quantity, decimal expected)
    {
        Money total = SalesViewModel.CalculateSaleTotal(
            Money.FromDecimal(35m), Quantity.Positive(quantity));

        Assert.Equal(expected, total.ToDecimal());
    }

    [Theory]
    [InlineData("35.00", 35)]
    [InlineData("35,00", 35)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    public void SalesPreview_ParsesDotAndCommaDecimalsWithoutMultiplyingThePrice(
        string input,
        decimal expected)
    {
        Assert.True(SalesViewModel.TryParseSaleDecimal(input, out decimal actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void VisibleIdentityAndSettings_FollowPhase46Contract()
    {
        string window = Read("src", "PeluqueriaAdmin.App", "MainWindow.xaml");
        string settings = Read("src", "PeluqueriaAdmin.App", "Views", "SettingsView.xaml");

        Assert.Contains("Title=\"\"", window, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Peluquería Admin\"", window, StringComparison.Ordinal);
        Assert.DoesNotContain("Exportar CSV", settings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presupuesto mensual", settings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Selector de moneda", settings, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exportar toda la información a Excel", settings, StringComparison.Ordinal);
        Assert.Contains("Cambiar carpeta", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void CaptureForm_PrecedesRecordsAndConsultButtonFollowsSelector()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        int datePicker = view.IndexOf("SelectedDate=\"{Binding SpecificDate}\"", StringComparison.Ordinal);
        int consult = view.IndexOf("Content=\"Consultar\"", datePicker, StringComparison.Ordinal);

        Assert.Contains("Border Grid.Row=\"6\"", view, StringComparison.Ordinal);
        Assert.Contains("TextBlock Grid.Row=\"7\"", view, StringComparison.Ordinal);
        Assert.True(datePicker >= 0 && consult > datePicker);
    }

    [Fact]
    public void HomeNotificationsAndAdaptivePeriods_AreVisibleAndAccessible()
    {
        string home = Read("src", "PeluqueriaAdmin.App", "Views", "HomeView.xaml");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");

        Assert.Contains("AutomationProperties.Name=\"Notificaciones de mantenimiento\"", home, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Notificaciones de obligaciones\"", home, StringComparison.Ordinal);
        Assert.Contains("MaintenanceNotificationCount", home, StringComparison.Ordinal);
        Assert.Contains("ObligationNotificationCount", home, StringComparison.Ordinal);
        Assert.Contains("Fecha específica", viewModel, StringComparison.Ordinal);
        Assert.Contains("Año específico", viewModel, StringComparison.Ordinal);
        Assert.Contains("00}:00", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void SalesPriceIsEditableAndEveryInputRefreshesPreview()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "SalesView.xaml");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "SalesViewModel.cs");

        Assert.DoesNotContain("IsReadOnly=\"True\" Text=\"{Binding UnitPriceText", view, StringComparison.Ordinal);
        Assert.Contains("OnQuantityTextChanged", viewModel, StringComparison.Ordinal);
        Assert.Contains("OnUnitPriceTextChanged", viewModel, StringComparison.Ordinal);
        Assert.Contains("quantity <= SelectedProduct.AvailableQuantity", viewModel, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        RepositoryFiles.Read(parts);
}
