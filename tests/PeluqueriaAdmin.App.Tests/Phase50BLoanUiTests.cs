namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase50BLoanUiTests
{
    [Fact]
    public void Loans_ExposeThreeMethodsDetailedPreviewAndSafeHistoryActions()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "ObligationsView.xaml");
        string viewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "ObligationsViewModel.cs");

        Assert.Contains("Interés fijo sobre capital inicial", viewModel, StringComparison.Ordinal);
        Assert.Contains("LoanPreviewInstallments", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Saldo capital\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Editar préstamo\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Guardar préstamo\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Eliminar préstamo\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Editar pago\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Guardar pago\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Eliminar pago\"", view, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedLoanPayment}\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Tasa mensual\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Tasa equivalente\"", view, StringComparison.Ordinal);
        Assert.Contains("Header=\"Descripción\"", view, StringComparison.Ordinal);
    }
}
