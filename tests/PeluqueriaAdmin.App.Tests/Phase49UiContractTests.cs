namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase49UiContractTests
{
    private static string View(params string[] parts) => RepositoryFiles.Read(parts);

    [Fact]
    public void Phase49_07_WorkerTableShowsCreditAndBothDifferentDates()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "LocalUseView.xaml");
        AssertOrder(text, "Header=\"Estado\"", "Header=\"Saldo a favor\"", "Header=\"Próximo cobro\"", "Header=\"Próximo pago requerido\"");
    }

    [Fact]
    public void Phase49_11_DistributionShowsTotalContributedBesideMonthlyPayment()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "CollaboratorsView.xaml");
        AssertOrder(text, "Header=\"Pago del mes\"", "Header=\"Total aportado\"", "Header=\"Estado del mes\"");
    }

    [Fact]
    public void Phase49_12_TablesHaveStableColumnsAndHorizontalScrolling()
    {
        string app = View("src", "PeluqueriaAdmin.App", "App.xaml");
        Assert.Contains("<Setter Property=\"CanUserResizeColumns\" Value=\"False\" />", app, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\" />", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_15_InventoryPlannedSelectorIsEditableAndSearchable()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        Assert.Contains("IsEditable=\"True\"", text, StringComparison.Ordinal);
        Assert.Contains("IsTextSearchEnabled=\"True\"", text, StringComparison.Ordinal);
        Assert.Contains("PendingMonthlyPurchaseRows", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_16_InventoryViewModelUsesAtomicPlannedProductLink()
    {
        string text = View("src", "PeluqueriaAdmin.App", "ViewModels", "InventoryViewModel.cs");
        Assert.Contains("SelectedMonthlyPlanId", text, StringComparison.Ordinal);
        Assert.Contains("PendingMonthlyPurchaseRows.Clear()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_17_PhysicalCountIsAbsentFromAddInventoryInterfaces()
    {
        string inventory = View("src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        string administration = View("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        Assert.DoesNotContain("Conteo físico", inventory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Actualizar existencia mediante conteo", administration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase49_18_RegisterConsumptionIsAbsentFromAddInventoryInterfaces()
    {
        string inventory = View("src", "PeluqueriaAdmin.App", "Views", "InventoryView.xaml");
        string administration = View("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        Assert.DoesNotContain("Registrar consumo", inventory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Registrar consumo", administration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase49_27_HomeBlockIsNamedPendingPayments()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "HomeView.xaml");
        Assert.Contains("Header=\"Pagos pendientes\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Servicios e impuestos pendientes", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_28_LoanInstallmentDescriptionIsVisible()
    {
        string home = View("src", "PeluqueriaAdmin.App", "Views", "HomeView.xaml");
        string obligations = View("src", "PeluqueriaAdmin.App", "Views", "ObligationsView.xaml");
        Assert.Contains("Binding=\"{Binding Description}\" Header=\"Descripción\"", home, StringComparison.Ordinal);
        Assert.Contains("Header=\"Pagos de préstamos\"", obligations, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_29_AnnualBalanceHidesTheGenericPeriodSelector()
    {
        string text = View("src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");
        Assert.Contains("ShowPeriodSelector => Title != AnnualBalanceModule", text, StringComparison.Ordinal);
        Assert.Contains("ShowAnnualClose => Title == AnnualBalanceModule", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_30_AnnualBalanceQueriesOnlyAYear()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        Assert.Contains("Text=\"Año a consultar\"", text, StringComparison.Ordinal);
        Assert.Contains("ShowSpecificYearQuery", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_31_AnnualChartBuildsTwelveIncomeBars()
    {
        string text = View("src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");
        Assert.Contains("Title = \"Ingresos\"", text, StringComparison.Ordinal);
        Assert.Contains("foreach (AnnualMonthFinancial month in report.Months)", text, StringComparison.Ordinal);
        Assert.Contains("incomes.Items.Add(new BarItem(month.IncomeMinorUnits / 100d))", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_32_AnnualChartBuildsTwelveOutflowBars()
    {
        string text = View("src", "PeluqueriaAdmin.App", "ViewModels", "AdministrationViewModel.cs");
        Assert.Contains("Title = \"Egresos\"", text, StringComparison.Ordinal);
        Assert.Contains("outflows.Items.Add(new BarItem(month.OutflowMinorUnits / 100d))", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_38_SettingsDoesNotContainTheMonthlyFinancialSummary()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "SettingsView.xaml");
        Assert.DoesNotContain("Resumen financiero del mes", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Punto de equilibrio", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase49_39_MonthlySummaryKeepsOneFinancialCloseBlock()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "AdministrationView.xaml");
        Assert.Equal(1, text.Split("Text=\"Cierre mensual\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("FinancialMonthRows", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_40_UnofficialExpensesHaveNoPeriodFilter()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "SettingsView.xaml");
        Assert.DoesNotContain("SelectedPeriod", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Periodo a mostrar", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_41_UnofficialExpenseHasExplicitEditAndSave()
    {
        string text = View("src", "PeluqueriaAdmin.App", "Views", "SettingsView.xaml");
        Assert.Contains("Content=\"Editar seleccionado\"", text, StringComparison.Ordinal);
        Assert.Contains("Content=\"Guardar edición\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase49_47_RefreshClearsCollectionsBeforeRefilling()
    {
        string home = View("src", "PeluqueriaAdmin.App", "ViewModels", "MainViewModel.cs");
        string obligations = View("src", "PeluqueriaAdmin.App", "ViewModels", "ObligationsViewModel.cs");
        Assert.Contains(".Clear()", home, StringComparison.Ordinal);
        Assert.Contains("LoanInstallments.Clear()", obligations, StringComparison.Ordinal);
        Assert.Contains("LoanPayments.Clear()", obligations, StringComparison.Ordinal);
    }

    private static void AssertOrder(string text, params string[] values)
    {
        int previous = -1;
        foreach (string value in values)
        {
            int current = text.IndexOf(value, StringComparison.Ordinal);
            Assert.True(current > previous, $"No se encontró '{value}' en el orden esperado.");
            previous = current;
        }
    }
}
