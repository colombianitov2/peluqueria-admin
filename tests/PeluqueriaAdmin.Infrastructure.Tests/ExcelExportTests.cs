using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Exporting;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Drafts;
using PeluqueriaAdmin.Infrastructure.Exporting;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class ExcelExportTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 21, 45, 30, TimeSpan.FromHours(-5));

    [Fact]
    public async Task CompleteExport_IsValidTypedUniqueAndContainsHistoryFutureDeletedAndDrafts()
    {
        string root = CreateRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            TestDbContextFactory factory = await PrepareDatabaseAsync(root, cancellationToken);
            string desktop = Path.Combine(root, "EscritorioPruebas");
            Directory.CreateDirectory(desktop);
            var service = CreateService(factory, desktop, new ClosedXmlWorkbookWriter());

            ExcelExportResult first = await service.ExportAsync(cancellationToken);
            ExcelExportResult second = await service.ExportAsync(cancellationToken);

            Assert.EndsWith("PeluqueriaAdmin-2026-07-18_21-45-30.xlsx", first.FilePath, StringComparison.Ordinal);
            Assert.EndsWith("PeluqueriaAdmin-2026-07-18_21-45-30-2.xlsx", second.FilePath, StringComparison.Ordinal);
            Assert.Equal(".xlsx", Path.GetExtension(first.FilePath));
            Assert.True(File.Exists(first.FilePath));
            using var workbook = new XLWorkbook(first.FilePath);
            string[] requiredSheets =
            [
                "Resumen general", "Ajustes", "Uso del local", "Cuotas semanales",
                "Precio sugerido por silla", "Sillas", "Asignaciones actuales",
                "Pagos por uso del local", "Historial trabajadores", "Colaboradores", "Aportes colaboradores", "Historial colaboradores", "Ventas", "Productos",
                "Inventario actual", "Movimientos de inventario", "Planes de reposición",
                "Otros ingresos", "Gastos", "Imprevistos", "Gastos extraoficiales", "Obligaciones",
                "Pagos de obligaciones", "Mantenimiento", "Cierres mensuales",
                "Distribuciones a colaboradores", "Pagos a colaboradores",
                "Resúmenes mensuales", "Balance anual", "Actividad e historial", "Historial fin. colaboradores", "Historial eliminado",
                "Borradores sin finalizar",
            ];
            Assert.All(requiredSheets, sheet => Assert.True(workbook.TryGetWorksheet(sheet, out _), sheet));
            Assert.All(workbook.Worksheets, sheet => Assert.True(sheet.AutoFilter.IsEnabled || sheet.Tables.Any(), sheet.Name));
            Assert.All(workbook.Worksheets, sheet => Assert.True(sheet.SheetView.SplitRow >= 1, sheet.Name));

            IXLWorksheet settings = workbook.Worksheet("Ajustes");
            Assert.Equal(XLDataType.Number, settings.Cell(2, 2).DataType);
            Assert.Equal(XLDataType.Number, settings.Cell(3, 2).DataType);
            Assert.Contains('%', settings.Cell(3, 2).Style.NumberFormat.Format);
            Assert.Equal(XLDataType.DateTime, settings.Cell(6, 2).DataType);
            Assert.Contains("yy", settings.Cell(6, 2).Style.NumberFormat.Format, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2026, settings.Cell(6, 2).GetDateTime().Year);
            Assert.False(workbook.TryGetWorksheet("Flujo de caja", out _));

            IXLWorksheet products = workbook.Worksheet("Productos");
            Assert.Equal("=SUM(1,1)", products.Cell(2, 1).GetString());
            Assert.Equal(XLDataType.Text, products.Cell(2, 1).DataType);
            Assert.True(products.Cell(2, 1).Style.IncludeQuotePrefix);
            Assert.False(products.Cell(2, 1).HasFormula);
            Assert.DoesNotContain("Unidad", products.Row(1).CellsUsed().Select(cell => cell.GetString()));
            IXLWorksheet contributions = workbook.Worksheet("Aportes colaboradores");
            Assert.Equal(XLDataType.DateTime, contributions.Cell(2, 1).DataType);
            Assert.Equal(XLDataType.Number, contributions.Cell(2, 3).DataType);
            Assert.Contains("no operativa", contributions.Cell(2, 6).GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("2027", workbook.Worksheet("Obligaciones").Cell(2, 3).GetFormattedString());
            Assert.Contains("Eliminado lógicamente", workbook.Worksheet("Historial eliminado").Column(4).CellsUsed().Select(x => x.GetString()));
            Assert.Contains("Aporte de colaborador", workbook.Worksheet("Historial eliminado").Column(1).CellsUsed().Select(x => x.GetString()));
            Assert.Contains("no finalizado", workbook.Worksheet("Borradores sin finalizar").Cell(2, 3).GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("+borrador", workbook.Worksheet("Otros ingresos").Column(2).CellsUsed().Select(x => x.GetString()));

            AdministrationData data = await new EfAdministrationRepository(factory).LoadAsync(cancellationToken);
            GeneralSettings generalSettings = await new EfSettingsRepository(factory).GetAsync(cancellationToken);
            LocalUsePerson person = data.LocalUsePeople.Single();
            decimal expectedDebt = WeeklyChargeCalculator.CalculateDebt(
                data.WeeklyCharges.Where(x => x.PersonId == person.Id),
                data.LocalUsePayments.Where(x => x.PersonId == person.Id)).ToDecimal();
            IXLRow useRow = workbook.Worksheet("Uso del local").RowsUsed().Single(row => row.Cell(1).GetString() == person.Name);
            Assert.Equal(expectedDebt, useRow.Cell(7).GetValue<decimal>());

            Product product = data.Products.Single();
            decimal expectedInventory = InventoryCalculator.CurrentQuantity(data.InventoryMovements.Where(x => x.ProductId == product.Id));
            IXLRow inventoryRow = workbook.Worksheet("Inventario actual").RowsUsed().Single(row => row.Cell(1).GetString() == product.Name);
            Assert.Equal(expectedInventory, inventoryRow.Cell(3).GetValue<decimal>());

            var july = new YearMonth(2026, 7);
            MonthlySummaryResult expectedMonth = AdministrationReports.MonthlySummary(
                data, generalSettings.OptionalSuppliesMonthlyBudget, generalSettings.CollaboratorProfit, july);
            IXLRow monthlyRow = workbook.Worksheet("Resúmenes mensuales").RowsUsed()
                .Single(row => row.Cell(1).DataType == XLDataType.DateTime && row.Cell(1).GetDateTime().Date == july.FirstDay.ToDateTime(TimeOnly.MinValue));
            Assert.Equal(expectedMonth.IncomeMinorUnits / 100m, monthlyRow.Cell(2).GetValue<decimal>());
            Assert.Equal(expectedMonth.GoalMinorUnits / 100m, monthlyRow.Cell(3).GetValue<decimal>());

            AnnualBalanceResult expectedAnnual = AdministrationReports.Annual(
                data, generalSettings.OptionalSuppliesMonthlyBudget, generalSettings.CollaboratorProfit, 2026).Balance;
            IXLRow annualRow = workbook.Worksheet("Balance anual").RowsUsed().Single(row => row.Cell(1).TryGetValue(out int year) && year == 2026);
            Assert.Equal(expectedAnnual.IncomeMinorUnits / 100m, annualRow.Cell(2).GetValue<decimal>());
            Assert.Equal(expectedAnnual.ExpenseMinorUnits / 100m, annualRow.Cell(3).GetValue<decimal>());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task FailedWriter_RemovesTemporaryAndLeavesNoPartialWorkbook()
    {
        string root = CreateRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            TestDbContextFactory factory = await PrepareDatabaseAsync(root, cancellationToken);
            string desktop = Path.Combine(root, "EscritorioPruebas");
            Directory.CreateDirectory(desktop);
            var service = CreateService(factory, desktop, new FailingWriter());

            await Assert.ThrowsAsync<IOException>(() => service.ExportAsync(cancellationToken));

            Assert.Empty(Directory.EnumerateFiles(desktop));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static async Task<TestDbContextFactory> PrepareDatabaseAsync(string root, CancellationToken cancellationToken)
    {
        ApplicationPaths paths = ApplicationPaths.FromRoot(root);
        paths.EnsureDirectories();
        var factory = new TestDbContextFactory(paths.DatabaseFilePath);
        var clock = new FixedTimeProvider(Now);
        await new DatabaseInitializer(factory, paths, clock).InitializeAsync(cancellationToken);
        var repository = new EfAdministrationRepository(factory);
        var service = new AdministrationService(repository, new EfSettingsRepository(factory), clock);
        DateTime utc = Now.UtcDateTime;

        var chair = Chair.Create("Silla principal", new DateOnly(2026, 6, 1), "Junto a la ventana", utc);
        await service.AddChairAsync(chair, cancellationToken);
        var person = LocalUsePerson.Create("Persona histórica", new DateOnly(2026, 6, 1), null, utc, "Trabajador vigente");
        await service.AddLocalUsePersonWithChairAsync(person, chair.Id, new DateOnly(2026, 7, 18), cancellationToken);
        var product = Product.Create("=SUM(1,1)", ProductCategory.ProductForSale, "unidad", utc, Money.FromDecimal(10m), "Producto de prueba");
        await service.AddProductAsync(product, cancellationToken);
        await service.AddInventoryMovementAsync(InventoryMovement.Initial(product.Id, new DateOnly(2026, 6, 1), Quantity.Positive(10), Money.FromDecimal(100), utc), cancellationToken);
        await service.AddInventoryMovementAsync(InventoryMovement.Sale(product.Id, new DateOnly(2026, 7, 1), Quantity.Positive(2), Money.FromDecimal(20), Money.FromDecimal(10), 10, utc), cancellationToken);
        await service.AddAsync(FinancialEntry.CreateIncome(new DateOnly(2025, 12, 1), "Ingreso histórico", Money.FromDecimal(50), utc), cancellationToken);
        var deleted = FinancialEntry.CreateIncome(new DateOnly(2026, 1, 1), "Registro eliminado", Money.FromDecimal(1), utc);
        await service.AddAsync(deleted, cancellationToken);
        await service.DeleteAsync(deleted, cancellationToken);
        await service.AddObligationAsync(Obligation.Create("Impuesto futuro", ObligationType.Tax, new DateOnly(2027, 1, 15), Money.FromDecimal(250), RecurrenceFrequency.None, utc), new DateOnly(2027, 1, 15), cancellationToken);
        await service.AddAsync(MaintenanceRecord.Create("Silla principal", "Preventivo", new DateOnly(2027, 2, 1), Money.FromDecimal(30), null, null, utc), cancellationToken);
        Collaborator collaborator = Collaborator.Create("Colaboradora", new DateOnly(2026, 1, 1), null, utc);
        await service.AddAsync(collaborator, cancellationToken);
        await service.AddCollaboratorContributionAsync(CollaboratorContribution.Create(
            collaborator.Id, new DateOnly(2026, 7, 2), Money.FromDecimal(500m), "Capital activo", utc), cancellationToken);
        CollaboratorContribution deletedContribution = CollaboratorContribution.Create(
            collaborator.Id, new DateOnly(2026, 7, 3), Money.FromDecimal(25m), "Capital eliminado", utc);
        await service.AddCollaboratorContributionAsync(deletedContribution, cancellationToken);
        await service.DeleteAsync(deletedContribution, cancellationToken);
        await service.AddUnofficialExpenseAsync(UnlistedExpense(), cancellationToken);
        await new EfFormDraftStore(factory).UpsertAsync(FormDraft.Create("Otros ingresos:nuevo", "Otros ingresos", "Registrar ingreso", "{\"concepto\":\"+borrador\"}", null, false, utc), cancellationToken);
        return factory;

        UnofficialExpense UnlistedExpense() => UnofficialExpense.Create(
            "Arriendo no registrado", Money.FromDecimal(30m), new DateOnly(2026, 7, 1), "Solo para el precio sugerido", utc);
    }

    private static ExcelExportService CreateService(TestDbContextFactory factory, string desktop, IExcelWorkbookWriter writer) =>
        new(factory, new FixedDesktopPath(desktop), writer, new FixedTimeProvider(Now));

    private static string CreateRoot() => Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));

    private static void Cleanup(string root)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class FixedDesktopPath(string path) : IUserDesktopPath
    {
        public string GetDesktopPath() => path;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("Prueba", TimeSpan.FromHours(-5), "Prueba", "Prueba");
    }

    private sealed class FailingWriter : IExcelWorkbookWriter
    {
        public void Save(XLWorkbook workbook, string path)
        {
            File.WriteAllText(path, "archivo parcial");
            throw new IOException("Falla simulada después de crear el temporal.");
        }
    }

    private sealed class TestDbContextFactory(string databasePath) : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = CreateOptions(databasePath);
        public PeluqueriaDbContext CreateDbContext() => new(options);
        public Task<PeluqueriaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
        private static DbContextOptions<PeluqueriaDbContext> CreateOptions(string databasePath)
        {
            var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(builder, databasePath);
            return builder.Options;
        }
    }
}
