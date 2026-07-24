using System.IO;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase41UiContractTests
{
    [Fact]
    public void VisibleUi_HasNoCashFlowOrTechnicalDraftRecoveryLanguage()
    {
        string main = Read("src", "PeluqueriaAdmin.App", "ViewModels", "MainViewModel.cs");
        string administration = Read("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");

        Assert.DoesNotContain("Flujo de caja", main, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Borrador recuperado", administration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Descartar borrador", administration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Limpiar formulario", administration, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectForms_DoNotExposeAGenericActionComboBoxOrUnitOfMeasure()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");

        Assert.DoesNotContain("Unidad de medida", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Text=\"Acción\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Registrar compra", viewModel, StringComparison.Ordinal);
        Assert.Contains("Cantidad a vender", viewModel, StringComparison.Ordinal);
        Assert.Contains("Existencia disponible", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_ExposeUsdAsTheOnlyCurrencyAndNoOptionalBudget()
    {
        string view = Read("src", "PeluqueriaAdmin.App", "Views", "SettingsView.xaml");
        string viewModel = Read("src", "PeluqueriaAdmin.App", "ViewModels", "SettingsViewModel.cs");

        Assert.DoesNotContain("\"COP\"", viewModel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CurrencyOptions", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Presupuesto mensual", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TotalChairs", view, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        RepositoryFiles.Read(parts);
}
