using System.IO;
using System.Text.RegularExpressions;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase47UiContractTests
{
    [Fact]
    public void EveryVisibleDataGrid_UsesSharedLockedColumnsAndInternalScrollbars()
    {
        string app = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "App.xaml");
        Assert.Contains("Property=\"CanUserResizeColumns\" Value=\"False\"", app, StringComparison.Ordinal);
        Assert.Contains("Property=\"CanUserResizeRows\" Value=\"False\"", app, StringComparison.Ordinal);
        Assert.Contains("Property=\"CanUserReorderColumns\" Value=\"False\"", app, StringComparison.Ordinal);
        Assert.Contains("Property=\"ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\"", app, StringComparison.Ordinal);
        Assert.Contains("Property=\"ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", app, StringComparison.Ordinal);

        string[] xamlFiles = Directory.EnumerateFiles(
            Path.Combine(RepositoryFiles.Root, "src", "PeluqueriaAdmin.App"), "*.xaml", SearchOption.AllDirectories).ToArray();
        Assert.All(xamlFiles, file =>
        {
            string xaml = File.ReadAllText(file);
            Assert.DoesNotMatch(new Regex("<DataGridTextColumn[^>]*Width=\\\"(?:\\d*\\*)\\\"", RegexOptions.CultureInvariant), xaml);
            Assert.DoesNotContain("Limpiar formulario", xaml, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Home_KeepsOnlyWideMaintenanceNotificationWithFixedColumns()
    {
        string home = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "HomeView.xaml");
        Assert.Contains("AutomationProperties.Name=\"Notificaciones de mantenimiento\"", home, StringComparison.Ordinal);
        Assert.Contains("Width=\"660\"", home, StringComparison.Ordinal);
        Assert.Contains("ElementStyle=\"{StaticResource WrappingCellText}\"", home, StringComparison.Ordinal);
        Assert.Contains("Content=\"Ir a Mantenimiento\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("ObligationNotification", home, StringComparison.Ordinal);
        Assert.DoesNotContain("Notificaciones de obligaciones", home, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryNotesObligationsAndMaintenance_FollowPhase47Layout()
    {
        string inventory = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        Assert.True(inventory.IndexOf("Header=\"Inventario actual\"", StringComparison.Ordinal)
            < inventory.IndexOf("Header=\"Movimientos\"", StringComparison.Ordinal));
        Assert.True(inventory.IndexOf("Header=\"Movimientos\"", StringComparison.Ordinal)
            < inventory.IndexOf("Header=\"Agregar al inventario\"", StringComparison.Ordinal));
        Assert.True(inventory.IndexOf("Header=\"Agregar al inventario\"", StringComparison.Ordinal)
            < inventory.IndexOf("Header=\"Lista mensual de compra\"", StringComparison.Ordinal));
        Assert.Contains("SelectedIndex=\"0\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Productos agregados recientemente", inventory, StringComparison.Ordinal);
        Assert.DoesNotContain("Planes de reposición", inventory, StringComparison.OrdinalIgnoreCase);

        string main = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "ViewModels", "MainViewModel.cs");
        Assert.True(main.IndexOf("\"Ajustes\"", StringComparison.Ordinal)
            < main.IndexOf("\"Notas\"", StringComparison.Ordinal));
        string notes = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "NotesView.xaml");
        Assert.Contains("AcceptsReturn=\"True\"", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Guardar", notes, StringComparison.Ordinal);

        string obligations = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "ObligationsView.xaml");
        Assert.Contains("Content=\"Agregar obligación\"", obligations, StringComparison.Ordinal);
        Assert.Contains("Content=\"Registrar pago\"", obligations, StringComparison.Ordinal);
        Assert.Contains("Header=\"Obligaciones registradas\"", obligations, StringComparison.Ordinal);
        Assert.Contains("Header=\"Pagos registrados\"", obligations, StringComparison.Ordinal);

        string maintenance = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "MaintenanceView.xaml");
        Assert.Contains("Header=\"Programar mantenimiento\"", maintenance, StringComparison.Ordinal);
        Assert.Contains("Header=\"Completar mantenimiento\"", maintenance, StringComparison.Ordinal);
        Assert.Contains("Header=\"Equipos para mantenimiento\"", maintenance, StringComparison.Ordinal);
        Assert.Contains("Header=\"Historial de mantenimiento de equipos\"", maintenance, StringComparison.Ordinal);
        Assert.Contains("HistoryAssetOptions", maintenance, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpensePie_HasExternalScrollableLegendAndNoSliceLabels()
    {
        string view = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        string viewModel = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");
        Assert.Contains("ItemsSource=\"{Binding ExpenseLegendRows}\"", view, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", view, StringComparison.Ordinal);
        Assert.Contains("InsideLabelFormat = string.Empty", viewModel, StringComparison.Ordinal);
        Assert.Contains("OutsideLabelFormat = string.Empty", viewModel, StringComparison.Ordinal);
        Assert.Contains("if (pie.Slices.Count > 0)", viewModel, StringComparison.Ordinal);
    }
}
