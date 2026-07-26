namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase50BFinancialUiTests
{
    [Fact]
    public void MonthlyAndAnnualViewsUseClearTermsAndRequiredCharts()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        string viewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");

        Assert.DoesNotContain("BarSeries", viewModel, StringComparison.Ordinal);
        Assert.Contains("AnnualIncomeCompositionChart", view, StringComparison.Ordinal);
        Assert.Contains("AnnualExpenseCompositionChart", view, StringComparison.Ordinal);
        Assert.Contains("Ingresos frente a egresos en el tiempo", view, StringComparison.Ordinal);
        Assert.Contains("new PieSeries", viewModel, StringComparison.Ordinal);
        Assert.Contains("var incomeLine = new LineSeries", viewModel, StringComparison.Ordinal);
        Assert.Contains("var expenseLine = new LineSeries", viewModel, StringComparison.Ordinal);
        Assert.Contains("Title = \"Ingresos vs. egresos\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Reservas nuevas\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Reservas arrastradas\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Ajustes de reservas\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Resultado distribuible\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Fondo de colaboradores\"", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Retenido por el local\"", viewModel, StringComparison.Ordinal);
        Assert.Contains("Content=\"Reabrir año\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void NotesHaveUnlimitedNoWrapEditingAndBothScrollbars()
    {
        string notes = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "NotesView.xaml");

        Assert.Contains("TextWrapping=\"NoWrap\"", notes, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", notes, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxLength=", notes, StringComparison.Ordinal);
    }
}
