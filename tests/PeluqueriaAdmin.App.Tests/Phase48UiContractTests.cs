namespace PeluqueriaAdmin.App.Tests;

using PeluqueriaAdmin.App.ViewModels;

public sealed class Phase48UiContractTests
{
    [Fact]
    public void FinancialSummaryRow_FormatsNegativeResultsWithoutCreatingInvalidDomainMoney()
    {
        FinancialSummaryRow row = FinancialSummaryRow.Create("Resultado distribuible", "USD", -1_234);

        Assert.Equal("Resultado distribuible", row.Concept);
        Assert.Contains("-12", row.Amount, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalUseProfile_ExposesOnlyChairAndLogicalDeletionFlows()
    {
        string view = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "LocalUseView.xaml");
        string viewModel = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "ViewModels", "LocalUseViewModel.cs");

        Assert.Contains("Header=\"Silla\"", view, StringComparison.Ordinal);
        Assert.Contains("Eliminar trabajador", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Silla y retiro", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Retirar trabajador del local", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RetirementDate", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("RetireWorkerCommand", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void CollaboratorProfile_HasExactPercentageLabelFullPaymentAndOneChronologicalHistory()
    {
        string view = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "CollaboratorsView.xaml");

        Assert.Contains("Porcentaje de ganancia asignado al colaborador (%)", view, StringComparison.Ordinal);
        Assert.Contains("Pagar ganancia completa", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Porcentaje de ganancia\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Pago del mes\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Participación pendiente", view, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, view.Split("Historial cronológico", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void FinancialCloseHomeSettingsAndAnnualViews_ExposePhase48Controls()
    {
        string administration = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        string home = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "HomeView.xaml");
        string settings = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "SettingsView.xaml");

        Assert.Contains("Text=\"Cierre mensual\"", administration, StringComparison.Ordinal);
        Assert.Contains("Content=\"Guardar exclusiones\"", administration, StringComparison.Ordinal);
        Assert.Contains("Content=\"Cerrar mes\"", administration, StringComparison.Ordinal);
        Assert.Contains("Content=\"Reabrir mes\"", administration, StringComparison.Ordinal);
        Assert.Contains("Content=\"Cerrar año\"", administration, StringComparison.Ordinal);
        Assert.Contains("Header=\"Movimientos del día\"", home, StringComparison.Ordinal);
        Assert.Contains("Text=\"Resumen financiero del mes\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Punto de equilibrio mensual\" /><TextBox", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryAndObligations_ExposeMonthlyPurchasesAndLoansInsideExistingModules()
    {
        string inventory = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        string obligations = RepositoryFiles.Read("src", "PeluqueriaAdmin.App", "Views", "ObligationsView.xaml");

        Assert.Contains("Header=\"Lista mensual de compra\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Reservar cuando el inventario llegue a cero", inventory, StringComparison.Ordinal);
        Assert.Contains("Content=\"Préstamos\"", obligations, StringComparison.Ordinal);
        Assert.Contains("Content=\"Registrar cuota\"", obligations, StringComparison.Ordinal);
        Assert.Contains("Header=\"Historial de cuotas\"", obligations, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"TextWrapping\" Value=\"Wrap\" />", obligations, StringComparison.Ordinal);
        Assert.Contains("Width=\"175\"", obligations, StringComparison.Ordinal);
    }
}
