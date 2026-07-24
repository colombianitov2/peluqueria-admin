using System.Text.Json;
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
using PeluqueriaAdmin.Domain.Notes;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Drafts;
using PeluqueriaAdmin.Infrastructure.Exporting;
using PeluqueriaAdmin.Infrastructure.Notes;
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
            Assert.Equal(2, Directory.EnumerateFiles(desktop, "*.xlsx", SearchOption.TopDirectoryOnly).Count());
            Assert.Empty(Directory.EnumerateFiles(desktop, "*.csv", SearchOption.TopDirectoryOnly));
            using var workbook = new XLWorkbook(first.FilePath);
            string[] requiredSheets =
            [
                "Resumen general", "Ajustes", "Notas", "Uso del local", "Cuotas semanales",
                "Tarifas semanales", "Precio sugerido por silla", "Sillas", "Asignaciones actuales",
                "Pagos por uso del local", "Historial trabajadores", "Colaboradores", "Aportes colaboradores", "Historial colaboradores", "Ventas", "Productos",
                "Inventario actual", "Movimientos de inventario", "Lista mensual de compra",
                "Planes de reposición", "Compatibilidad inventario",
                "Otros ingresos", "Gastos", "Imprevistos", "Gastos extraoficiales", "Obligaciones",
                "Pagos de obligaciones", "Cuentas por cobrar", "Cuentas por pagar", "Préstamos", "Cuotas de préstamos", "Pagos de préstamos", "Mantenimiento", "Cierres mensuales", "Reservas financieras", "Exclusiones de cierre",
                "Distribuciones a colaboradores", "Pagos a colaboradores",
                "Resúmenes mensuales", "Balance anual", "Cierres anuales", "Saldos arrastrados", "Flujo de caja", "Movimientos generales", "Historial fin. colaboradores", "Historial eliminado",
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
            Assert.Equal("Cantidad de sillas heredada", settings.Cell(7, 1).GetString());
            Assert.Equal(XLDataType.Number, settings.Cell(7, 2).DataType);
            Assert.Equal("Presupuesto opcional heredado", settings.Cell(8, 1).GetString());
            Assert.Equal(XLDataType.Number, settings.Cell(8, 2).DataType);
            Assert.Equal("Moneda persistida", settings.Cell(9, 1).GetString());
            Assert.Equal(XLDataType.DateTime, settings.Cell(10, 2).DataType);
            Assert.True(workbook.TryGetWorksheet("Flujo de caja", out _));
            IXLWorksheet weeklyRates = workbook.Worksheet("Tarifas semanales");
            Assert.Equal("Vigente desde", weeklyRates.Cell(1, 1).GetString());
            Assert.Equal(XLDataType.DateTime, weeklyRates.Cell(2, 1).DataType);
            Assert.Equal(XLDataType.Number, weeklyRates.Cell(2, 2).DataType);
            Assert.Contains("0.00", weeklyRates.Cell(2, 2).Style.NumberFormat.Format, StringComparison.Ordinal);

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
            IXLWorksheet deletedHistory = workbook.Worksheet("Historial eliminado");
            Assert.Equal(
                ["Tipo de registro", "Nombre o concepto", "Detalle de negocio", "Registro relacionado", "Fecha del negocio", "Valor", "Cantidad", "Fecha de creación", "Última actualización", "Fecha de eliminación", "Estado", "Contenido técnico completo (JSON; incluye identificadores)"],
                deletedHistory.Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray());
            Assert.Contains("Eliminado lógicamente", deletedHistory.Column(11).CellsUsed().Select(x => x.GetString()));
            Assert.Contains("Aporte de colaborador", deletedHistory.Column(1).CellsUsed().Select(x => x.GetString()));
            Assert.All(
                deletedHistory.RowsUsed().Skip(1),
                row => Assert.DoesNotMatch(
                    @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
                    string.Join("|", row.Cells(1, 11).Select(cell => cell.GetString()))));
            IXLRow deletedProductRow = deletedHistory.RowsUsed()
                .Single(row => row.Cell(1).GetString() == "Producto"
                    && row.Cell(2).GetString() == "Producto eliminado auditado");
            Assert.Contains("Categoría", deletedProductRow.Cell(3).GetString(), StringComparison.Ordinal);
            Assert.Equal(XLDataType.DateTime, deletedProductRow.Cell(8).DataType);
            Assert.Equal(XLDataType.DateTime, deletedProductRow.Cell(9).DataType);
            Assert.Equal(XLDataType.DateTime, deletedProductRow.Cell(10).DataType);
            using (JsonDocument payload = JsonDocument.Parse(deletedProductRow.Cell(12).GetString()))
            {
                Assert.Equal("Producto eliminado auditado", payload.RootElement.GetProperty("Name").GetString());
                Assert.True(payload.RootElement.TryGetProperty("Id", out JsonElement productId));
                Assert.NotEqual(Guid.Empty, productId.GetGuid());
                Assert.True(payload.RootElement.TryGetProperty("CreatedUtc", out _));
                Assert.True(payload.RootElement.TryGetProperty("UpdatedUtc", out _));
                Assert.True(payload.RootElement.TryGetProperty("DeletedUtc", out _));
            }
            IXLRow deletedMovementRow = deletedHistory.RowsUsed()
                .Single(row => row.Cell(1).GetString() == "Movimiento de inventario"
                    && row.Cell(4).GetString() == "Producto eliminado auditado");
            Assert.Equal("Producto eliminado auditado", deletedMovementRow.Cell(4).GetString());
            using (JsonDocument payload = JsonDocument.Parse(deletedMovementRow.Cell(12).GetString()))
            {
                Assert.True(payload.RootElement.TryGetProperty("ProductId", out JsonElement productId));
                Assert.NotEqual(Guid.Empty, productId.GetGuid());
                Assert.Equal(4m, payload.RootElement.GetProperty("QuantityDelta").GetDecimal());
            }
            Assert.Contains(
                deletedHistory.RowsUsed(),
                row => row.Cell(1).GetString() == "Obligación"
                    && row.Cell(2).GetString() == "Obligación eliminada auditada");
            Assert.Contains(
                deletedHistory.RowsUsed(),
                row => row.Cell(1).GetString() == "Pago de obligación"
                    && row.Cell(4).GetString() == "Obligación eliminada auditada");
            Assert.Contains(
                deletedHistory.RowsUsed(),
                row => row.Cell(1).GetString() == "Préstamo"
                    && row.Cell(2).GetString() == "Préstamo eliminado auditado");
            Assert.Contains(
                deletedHistory.RowsUsed(),
                row => row.Cell(1).GetString() == "Pago de préstamo"
                    && row.Cell(4).GetString() == "Préstamo eliminado auditado");
            Assert.Contains("Trabajador eliminado con historial", workbook.Worksheet("Cuotas semanales").Column(1).CellsUsed().Select(x => x.GetString()));
            Assert.Contains("Trabajador eliminado con historial", workbook.Worksheet("Pagos por uso del local").Column(1).CellsUsed().Select(x => x.GetString()));
            Assert.Contains("no finalizado", workbook.Worksheet("Borradores sin finalizar").Cell(2, 3).GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("+borrador", workbook.Worksheet("Otros ingresos").Column(2).CellsUsed().Select(x => x.GetString()));
            Assert.Equal("Texto persistente de prueba", workbook.Worksheet("Notas").Cell(2, 1).GetString());
            Assert.Equal("Porcentaje de ganancia", workbook.Worksheet("Colaboradores").Cell(1, 4).GetString());
            Assert.Equal("Préstamo anterior", workbook.Worksheet("Préstamos").Cell(2, 2).GetString());
            Assert.True(workbook.TryGetWorksheet("Cuotas de préstamos", out _));
            Assert.True(workbook.TryGetWorksheet("Pagos de préstamos", out _));
            IXLWorksheet monthlyPurchases = workbook.Worksheet("Lista mensual de compra");
            Assert.Equal(
                ["Mes", "Producto", "Categoría", "Cantidad", "Costo esperado unitario", "Total esperado", "Moneda", "Compra vinculada", "Descripción", "Estado"],
                monthlyPurchases.Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray());
            Assert.DoesNotContain("Activa", monthlyPurchases.Row(1).CellsUsed().Select(cell => cell.GetString()));
            Assert.DoesNotContain("Reserva al agotarse", monthlyPurchases.Row(1).CellsUsed().Select(cell => cell.GetString()));
            Assert.Equal(XLDataType.DateTime, monthlyPurchases.Cell(2, 1).DataType);
            Assert.Equal(XLDataType.Number, monthlyPurchases.Cell(2, 4).DataType);
            Assert.Equal(XLDataType.Number, monthlyPurchases.Cell(2, 5).DataType);
            Assert.Equal(XLDataType.Number, monthlyPurchases.Cell(2, 6).DataType);
            Assert.Equal(10m, monthlyPurchases.Cell(2, 6).GetValue<decimal>());
            Assert.Equal("Pendiente", monthlyPurchases.Cell(2, 10).GetString());
            IXLWorksheet inventoryCompatibility = workbook.Worksheet("Compatibilidad inventario");
            Assert.Contains(
                "Lista mensual vigente",
                inventoryCompatibility.Column(1).CellsUsed().Select(cell => cell.GetString()));
            Assert.Equal("Activa heredada", inventoryCompatibility.Cell(1, 5).GetString());
            Assert.Equal("Reserva al agotarse heredada", inventoryCompatibility.Cell(1, 6).GetString());
            Assert.Equal("No", inventoryCompatibility.Cell(2, 5).GetString());
            Assert.Equal("Sí", inventoryCompatibility.Cell(2, 6).GetString());
            Assert.Contains(
                inventoryCompatibility.Column(7).CellsUsed().Select(cell => cell.GetString()),
                value => value.Contains("ya no se editan ni afectan cálculos", StringComparison.OrdinalIgnoreCase));
            IXLWorksheet restockPlans = workbook.Worksheet("Planes de reposición");
            Assert.Equal(
                ["Mes", "Producto", "Cantidad requerida", "Existencia actual", "Compra sugerida", "Estado"],
                restockPlans.Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray());
            Assert.Equal(XLDataType.DateTime, restockPlans.Cell(2, 1).DataType);
            Assert.Equal(12m, restockPlans.Cell(2, 3).GetValue<decimal>());
            Assert.Equal("Histórico heredado", restockPlans.Cell(2, 6).GetString());
            Assert.Contains(
                workbook.Worksheet("Cuentas por pagar").RowsUsed(),
                row => row.Cell(1).GetString() == "Compra mensual"
                    && row.Cell(4).GetValue<decimal>() == 10m);

            IXLRow creditRow = workbook.Worksheet("Obligaciones").RowsUsed()
                .First(row => row.Cell(1).GetString() == "Crédito semanal de prueba");
            Assert.Equal("Crédito", creditRow.Cell(2).GetString());
            Assert.Equal("Semanal", creditRow.Cell(8).GetString());
            Assert.Equal(40m, creditRow.Cell(4).GetValue<decimal>());
            Assert.Equal(35m, creditRow.Cell(5).GetValue<decimal>());
            Assert.Equal(0m, creditRow.Cell(6).GetValue<decimal>());
            Assert.Equal("Pagado", creditRow.Cell(10).GetString());
            Assert.Equal(XLDataType.Number, workbook.Worksheet("Reservas financieras").Cell(2, 5).DataType);
            Assert.Equal(XLDataType.Number, workbook.Worksheet("Distribuciones a colaboradores").Cell(2, 3).DataType);
            Assert.Contains('%', workbook.Worksheet("Distribuciones a colaboradores").Cell(2, 3).Style.NumberFormat.Format);
            Assert.Equal("Importe", workbook.Worksheet("Movimientos generales").Cell(1, 7).GetString());
            Assert.Equal("Estado", workbook.Worksheet("Movimientos generales").Cell(1, 9).GetString());
            Assert.True(workbook.Worksheet("Notas").Cell(2, 1).Style.Alignment.WrapText);
            Assert.DoesNotContain(
                workbook.Worksheet("Tarifas semanales").Column(1).CellsUsed(),
                cell => cell.GetString() == "TOTAL");
            Assert.DoesNotContain(
                workbook.Worksheet("Precio sugerido por silla").Column(1).CellsUsed(),
                cell => cell.GetString() == "TOTAL");
            Assert.DoesNotContain(
                workbook.Worksheet("Productos").Column(1).CellsUsed(),
                cell => cell.GetString() == "TOTAL");
            IXLRow inventoryTotal = workbook.Worksheet("Inventario actual").RowsUsed()
                .Single(row => row.Cell(1).GetString() == "TOTAL");
            Assert.True(inventoryTotal.Cell(4).IsEmpty());
            Assert.Equal(XLDataType.Number, inventoryTotal.Cell(5).DataType);
            IXLRow monthlyPurchaseTotal = monthlyPurchases.RowsUsed()
                .Single(row => row.Cell(1).GetString() == "TOTAL");
            Assert.True(monthlyPurchaseTotal.Cell(5).IsEmpty());
            Assert.Equal(10m, monthlyPurchaseTotal.Cell(6).GetValue<decimal>());
            Assert.Contains(
                workbook.Worksheet("Pagos por uso del local").Column(1).CellsUsed(),
                cell => cell.GetString() == "TOTAL");

            AdministrationData data = await new EfAdministrationRepository(factory).LoadAsync(cancellationToken);
            GeneralSettings generalSettings = await new EfSettingsRepository(factory).GetAsync(cancellationToken);
            decimal expectedObligations = data.Obligations.Sum(obligation =>
                obligation.OutstandingAmount(data.ObligationPayments).ToDecimal());
            IXLRow obligationsSummaryRow = workbook.Worksheet("Resumen general").RowsUsed()
                .Single(row => row.Cell(1).GetString() == "Obligaciones pendientes");
            Assert.Equal(expectedObligations, obligationsSummaryRow.Cell(2).GetValue<decimal>());
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
            MonthlyClose? julyClose = data.MonthlyCloses.SingleOrDefault(x => x.Month == july && x.IsConfirmed);
            FinancialMonthSnapshot expectedMonth = julyClose?.ToFinancialSnapshot()
                ?? FinancialMonthCalculator.Calculate(data, generalSettings.CollaboratorProfit, july);
            IXLRow monthlyRow = workbook.Worksheet("Resúmenes mensuales").RowsUsed()
                .Single(row => row.Cell(1).DataType == XLDataType.DateTime && row.Cell(1).GetDateTime().Date == july.FirstDay.ToDateTime(TimeOnly.MinValue));
            Assert.Equal(expectedMonth.CollectedOperatingIncomeMinorUnits / 100m, monthlyRow.Cell(2).GetValue<decimal>());
            Assert.Equal(expectedMonth.AccountsReceivableMinorUnits / 100m, monthlyRow.Cell(3).GetValue<decimal>());

            IXLRow annualRow = workbook.Worksheet("Balance anual").RowsUsed().Single(row => row.Cell(1).TryGetValue(out int year) && year == 2026);
            AnnualFinancialReport expectedAnnual = AnnualFinancialCalculator.Calculate(
                data,
                generalSettings.CollaboratorProfit,
                2026,
                new DateOnly(2026, 7, 18));
            Assert.Contains(expectedAnnual.Months, month => month.IsClosed);
            Assert.Contains(
                expectedAnnual.Months,
                month => !month.IsClosed && month.Month.FirstDay <= new DateOnly(2026, 7, 18));
            Assert.Equal(expectedAnnual.IncomeMinorUnits / 100m, annualRow.Cell(2).GetValue<decimal>());
            Assert.Equal(expectedAnnual.OutflowMinorUnits / 100m, annualRow.Cell(3).GetValue<decimal>());
            Assert.Equal(expectedAnnual.ResultMinorUnits / 100m, annualRow.Cell(4).GetValue<decimal>());
            Assert.Equal(expectedAnnual.AccountsReceivableMinorUnits / 100m, annualRow.Cell(5).GetValue<decimal>());
            Assert.Equal(expectedAnnual.AccountsPayableMinorUnits / 100m, annualRow.Cell(6).GetValue<decimal>());
            Assert.Equal(expectedAnnual.PendingReservesMinorUnits / 100m, annualRow.Cell(7).GetValue<decimal>());
            Assert.Equal(expectedAnnual.PendingLoansMinorUnits / 100m, annualRow.Cell(8).GetValue<decimal>());
            Assert.Equal(expectedAnnual.CollaboratorFundMinorUnits / 100m, annualRow.Cell(9).GetValue<decimal>());
            Assert.Equal(expectedAnnual.SurplusMinorUnits / 100m, annualRow.Cell(10).GetValue<decimal>());
            Assert.Equal(expectedAnnual.DeficitMinorUnits / 100m, annualRow.Cell(11).GetValue<decimal>());
            Assert.Equal(expectedAnnual.ProjectedNextYearBalanceMinorUnits / 100m, annualRow.Cell(12).GetValue<decimal>());
            Assert.Contains("meses abiertos", annualRow.Cell(25).GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Créditos", workbook.Worksheet("Balance anual").Cell(1, 15).GetString());
            IXLRow annualCreditRow = workbook.Worksheet("Balance anual").RowsUsed()
                .Single(row => row.Cell(1).TryGetValue(out int year) && year == 2027);
            Assert.Equal(35m, annualCreditRow.Cell(15).GetValue<decimal>());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Phase49_46_ExportProducesOneXlsxAndZeroCsvFiles()
    {
        string root = CreateRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            TestDbContextFactory factory = await PrepareDatabaseAsync(root, cancellationToken);
            string desktop = Path.Combine(root, "EscritorioPruebas");
            Directory.CreateDirectory(desktop);

            await CreateService(factory, desktop, new ClosedXmlWorkbookWriter()).ExportAsync(cancellationToken);

            Assert.Single(Directory.EnumerateFiles(desktop, "*.xlsx", SearchOption.TopDirectoryOnly));
            Assert.Empty(Directory.EnumerateFiles(desktop, "*.csv", SearchOption.TopDirectoryOnly));
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
        var deletedChair = Chair.Create("Silla histórica", new DateOnly(2026, 6, 1), null, utc);
        await service.AddChairAsync(deletedChair, cancellationToken);
        var deletedPerson = LocalUsePerson.Create("Trabajador eliminado con historial", new DateOnly(2026, 6, 1), null, utc);
        await service.AddLocalUsePersonWithChairAsync(deletedPerson, deletedChair.Id, new DateOnly(2026, 7, 18), cancellationToken);
        await service.RegisterLocalUsePaymentAsync(deletedPerson.Id, new DateOnly(2026, 6, 8), Money.FromDecimal(12m), cancellationToken);
        await service.DeleteLocalUsePersonAsync(deletedPerson.Id, cancellationToken);
        var product = Product.Create("=SUM(1,1)", ProductCategory.ProductForSale, "unidad", utc, Money.FromDecimal(10m), "Producto de prueba");
        await service.AddProductAsync(product, cancellationToken);
        await service.AddInventoryMovementAsync(InventoryMovement.Initial(product.Id, new DateOnly(2026, 6, 1), Quantity.Positive(10), Money.FromDecimal(100), utc), cancellationToken);
        await service.AddInventoryMovementAsync(InventoryMovement.Sale(product.Id, new DateOnly(2026, 7, 1), Quantity.Positive(2), Money.FromDecimal(20), Money.FromDecimal(10), 10, utc), cancellationToken);
        var deletedProduct = Product.Create(
            "Producto eliminado auditado",
            ProductCategory.OtherProductForSale,
            "unidad",
            utc,
            Money.FromDecimal(9m),
            "Producto retirado con movimiento histórico",
            Money.FromDecimal(4m));
        await service.AddProductAsync(deletedProduct, cancellationToken);
        InventoryMovement deletedMovement = InventoryMovement.Initial(
            deletedProduct.Id,
            new DateOnly(2026, 5, 1),
            Quantity.Positive(4m),
            Money.FromDecimal(16m),
            utc,
            "Existencia histórica del producto eliminado");
        await service.AddInventoryMovementAsync(deletedMovement, cancellationToken);
        await service.DeleteInventoryMovementAsync(deletedMovement, cancellationToken);
        await service.DeleteAsync(deletedProduct, cancellationToken);
        await service.AddAsync(FinancialEntry.CreateIncome(new DateOnly(2025, 12, 1), "Ingreso histórico", Money.FromDecimal(50), utc), cancellationToken);
        var deleted = FinancialEntry.CreateIncome(new DateOnly(2026, 1, 1), "Registro eliminado", Money.FromDecimal(1), utc);
        await service.AddAsync(deleted, cancellationToken);
        await service.DeleteAsync(deleted, cancellationToken);
        await service.AddObligationAsync(Obligation.Create("Impuesto futuro", ObligationType.Tax, new DateOnly(2027, 1, 15), Money.FromDecimal(250), RecurrenceFrequency.None, utc), new DateOnly(2027, 1, 15), cancellationToken);
        Obligation credit = Obligation.Create(
            "Crédito semanal de prueba",
            ObligationType.Credit,
            new DateOnly(2027, 3, 1),
            Money.FromDecimal(40),
            RecurrenceFrequency.Weekly,
            utc,
            "Crédito futuro con recurrencia semanal");
        await service.AddObligationAsync(credit, new DateOnly(2027, 3, 15), cancellationToken);
        await service.RegisterObligationPaymentAsync(
            credit.SeriesId,
            new DateOnly(2027, 3, 1),
            Money.FromDecimal(35),
            "Pago real menor que el esperado, pero confirmado",
            cancellationToken);
        Obligation deletedObligation = Obligation.Create(
            "Obligación eliminada auditada",
            ObligationType.Service,
            new DateOnly(2026, 5, 10),
            Money.FromDecimal(22m),
            RecurrenceFrequency.None,
            utc,
            "Obligación retirada con pago histórico");
        await service.AddObligationAsync(
            deletedObligation,
            deletedObligation.DueDate,
            cancellationToken);
        ObligationPayment deletedObligationPayment = await service.RegisterObligationPaymentAsync(
            deletedObligation.SeriesId,
            new DateOnly(2026, 5, 10),
            Money.FromDecimal(22m),
            "Pago histórico de obligación eliminada",
            cancellationToken);
        await service.DeleteAsync(deletedObligationPayment, cancellationToken);
        await service.DeleteObligationSeriesAsync(deletedObligation.SeriesId, cancellationToken);
        await service.AddAsync(MaintenanceRecord.Create("Silla principal", "Preventivo", new DateOnly(2027, 2, 1), Money.FromDecimal(30), null, null, utc), cancellationToken);
        Collaborator collaborator = Collaborator.Create("Colaboradora", new DateOnly(2026, 1, 1), null, utc);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorFundParticipationAsync(
            collaborator.Id, Percentage.FromPercent(100m), cancellationToken);
        await service.AddCollaboratorContributionAsync(CollaboratorContribution.Create(
            collaborator.Id, new DateOnly(2026, 7, 2), Money.FromDecimal(500m), "Capital activo", utc), cancellationToken);
        CollaboratorContribution deletedContribution = CollaboratorContribution.Create(
            collaborator.Id, new DateOnly(2026, 7, 3), Money.FromDecimal(25m), "Capital eliminado", utc);
        await service.AddCollaboratorContributionAsync(deletedContribution, cancellationToken);
        await service.DeleteAsync(deletedContribution, cancellationToken);
        await service.AddMonthlyPurchaseItemAsync(MonthlyPurchaseItem.Create(
            product.Id, new YearMonth(2026, 7), 2, Money.FromDecimal(5m), false, true, utc,
            "Compra mensual de prueba"), cancellationToken);
        await service.AddAsync(MonthlyRestockPlan.Create(
            product.Id,
            new YearMonth(2026, 8),
            Quantity.NonNegative(12m),
            utc), cancellationToken);
        Loan loan = Loan.Create("Préstamo de prueba", Money.FromDecimal(100m), Money.FromDecimal(10m),
            new DateOnly(2026, 7, 1), LoanFrequency.Monthly, 10, new DateOnly(2026, 7, 31), utc,
            "Financiación separada");
        await service.AddLoanAsync(loan, cancellationToken);
        Loan deletedLoan = Loan.Create(
            "Préstamo eliminado auditado",
            Money.FromDecimal(60m),
            Money.FromDecimal(20m),
            new DateOnly(2026, 4, 1),
            LoanFrequency.Monthly,
            3,
            new DateOnly(2026, 4, 30),
            utc,
            "Préstamo retirado con pago histórico");
        await service.AddLoanAsync(deletedLoan, cancellationToken);
        LoanPayment deletedLoanPayment = await service.RegisterLoanPaymentAsync(
            deletedLoan.Id,
            new DateOnly(2026, 4, 30),
            Money.FromDecimal(20m),
            "Pago histórico de préstamo eliminado",
            cancellationToken);
        await service.DeleteAsync(deletedLoanPayment, cancellationToken);
        Loan currentDeletedLoan = (await repository.LoadAsync(cancellationToken)).Loans
            .Single(item => item.Id == deletedLoan.Id);
        await service.DeleteAsync(currentDeletedLoan, cancellationToken);
        await service.SetCloseExclusionAsync(new YearMonth(2026, 7), FinancialCommitmentSource.Obligation,
            Guid.NewGuid(), true, "Pendiente de verificación documental", cancellationToken);
        await service.CloseFinancialMonthAsync(new YearMonth(2026, 7), cancellationToken);
        await service.RegisterLoanPaymentAsync(loan.Id, new DateOnly(2026, 7, 31), Money.FromDecimal(10m),
            "Cuota real", cancellationToken);
        await service.AddAsync(AnnualClose.Create(2025, 50_000, 20_000, 5_000, 4_000, 3_000,
            5_000, 25_000, utc), cancellationToken);
        await service.AddUnofficialExpenseAsync(UnlistedExpense(), cancellationToken);
        await new EfFormDraftStore(factory).UpsertAsync(FormDraft.Create("Otros ingresos:nuevo", "Otros ingresos", "Registrar ingreso", "{\"concepto\":\"+borrador\"}", null, false, utc), cancellationToken);
        await new EfNoteRepository(factory).SaveAsync(AppNote.Create("Texto persistente de prueba", utc), cancellationToken);
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
