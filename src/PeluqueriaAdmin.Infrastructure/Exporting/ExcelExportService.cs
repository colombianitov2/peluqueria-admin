using System.Reflection;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Exporting;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Persistence;

namespace PeluqueriaAdmin.Infrastructure.Exporting;

public sealed class ExcelExportService(
    IDbContextFactory<PeluqueriaDbContext> contextFactory,
    IUserDesktopPath desktopPath,
    IExcelWorkbookWriter workbookWriter,
    TimeProvider timeProvider) : IExcelExportService
{
    private const string MoneyFormat = "#,##0.00;[Red]-#,##0.00";
    private const string QuantityFormat = "#,##0.###;[Red]-#,##0.###";
    private const string DateFormat = "yyyy-mm-dd";
    private const string DateTimeFormat = "yyyy-mm-dd hh:mm:ss";
    private static int tableSequence;

    public async Task<ExcelExportResult> ExportAsync(CancellationToken cancellationToken = default)
    {
        DateTime cutoffLocal = timeProvider.GetLocalNow().DateTime;
        DateOnly today = DateOnly.FromDateTime(cutoffLocal);
        ExportSnapshot snapshot = await ReadSnapshotAsync(cancellationToken);
        string version = ReadVersion();
        string desktop = desktopPath.GetDesktopPath();
        if (string.IsNullOrWhiteSpace(desktop))
        {
            throw new InvalidOperationException("Windows no informó una ruta válida para el Escritorio.");
        }

        Directory.CreateDirectory(desktop);
        string finalPath = UniquePath(desktop, $"PeluqueriaAdmin-{cutoffLocal:yyyy-MM-dd_HH-mm-ss}.xlsx");
        string temporaryPath = Path.Combine(desktop, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp.xlsx");

        try
        {
            using XLWorkbook workbook = BuildWorkbook(snapshot, cutoffLocal, today, version);
            workbookWriter.Save(workbook, temporaryPath);
            File.Move(temporaryPath, finalPath, false);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }

        return new ExcelExportResult(finalPath, cutoffLocal, version, snapshot.Settings.CurrencyCode.Value);
    }

    private async Task<ExportSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        await using PeluqueriaDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        GeneralSettings settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken);
        AdministrationData active = new(
            await db.LocalUsePeople.AsNoTracking().ToListAsync(cancellationToken),
            await db.WeeklyRates.AsNoTracking().ToListAsync(cancellationToken),
            await db.WeeklyCharges.AsNoTracking().ToListAsync(cancellationToken),
            await db.LocalUsePayments.AsNoTracking().ToListAsync(cancellationToken),
            await db.Products.AsNoTracking().ToListAsync(cancellationToken),
            await db.InventoryMovements.AsNoTracking().ToListAsync(cancellationToken),
            await db.RestockPlans.AsNoTracking().ToListAsync(cancellationToken),
            await db.FinancialEntries.AsNoTracking().ToListAsync(cancellationToken),
            await db.Obligations.AsNoTracking().ToListAsync(cancellationToken),
            await db.ObligationPayments.AsNoTracking().ToListAsync(cancellationToken),
            await db.MaintenanceRecords.AsNoTracking().ToListAsync(cancellationToken),
            await db.Collaborators.AsNoTracking().ToListAsync(cancellationToken),
            await db.MonthlyCloses.AsNoTracking().ToListAsync(cancellationToken),
            await db.MonthlyCloseParticipants.AsNoTracking().ToListAsync(cancellationToken),
            await db.DistributionPayments.AsNoTracking().ToListAsync(cancellationToken));

        List<DeletedRecord> deleted = [];
        deleted.AddRange((await db.LocalUsePeople.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Persona que usa el local", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.WeeklyRates.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Tarifa semanal", x.Amount.ToDecimal().ToString("0.00"), x.DeletedUtc)));
        deleted.AddRange((await db.WeeklyCharges.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Cuota semanal", $"{x.PeriodStart:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.LocalUsePayments.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Pago por uso del local", $"{x.PaymentDate:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.Products.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Producto", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.InventoryMovements.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Movimiento de inventario", $"{MovementName(x.Type)} · {x.QuantityDelta:0.###}", x.DeletedUtc)));
        deleted.AddRange((await db.RestockPlans.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Plan de reposición", x.Month.ToString(), x.DeletedUtc)));
        deleted.AddRange((await db.FinancialEntries.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted(SpanishText.For(x.Type), x.Concept, x.DeletedUtc)));
        deleted.AddRange((await db.Obligations.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Obligación", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.ObligationPayments.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Pago de obligación", $"{x.Date:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.MaintenanceRecords.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Mantenimiento", $"{x.Asset} · {x.MaintenanceType}", x.DeletedUtc)));
        deleted.AddRange((await db.Collaborators.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Colaborador", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.MonthlyCloses.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Cierre mensual", x.Month.ToString(), x.DeletedUtc)));
        deleted.AddRange((await db.MonthlyCloseParticipants.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Distribución a colaborador", x.Amount.ToDecimal().ToString("0.00"), x.DeletedUtc)));
        deleted.AddRange((await db.DistributionPayments.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Pago a colaborador", $"{x.Date:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));

        var drafts = await db.FormDrafts.AsNoTracking().OrderBy(x => x.UpdatedUtc).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ExportSnapshot(settings, active, deleted, drafts.Select(x => new DraftRecord(x.Module, x.FormType, x.IsEdit ? "Edición no finalizada" : "Nuevo registro no finalizado", x.PayloadJson, x.UpdatedUtc)).ToArray());
    }

    private static XLWorkbook BuildWorkbook(ExportSnapshot snapshot, DateTime cutoff, DateOnly today, string version)
    {
        tableSequence = 0;
        var workbook = new XLWorkbook();
        workbook.Properties.Title = "Exportación completa de Peluquería Admin";
        workbook.Properties.Subject = $"Fotografía de datos al {cutoff:yyyy-MM-dd HH:mm:ss}";
        workbook.Properties.Author = "Peluquería Admin";

        AdministrationData data = snapshot.Active;
        string currency = snapshot.Settings.CurrencyCode.Value;
        (DateOnly? from, DateOnly? to) = CoveredPeriod(data);

        AddSummary(workbook, snapshot, cutoff, today, version, currency, from, to);
        AddTable(workbook, "Ajustes", ["Ajuste", "Valor", "Unidad"],
        [
            ["Valor semanal general por uso del local", snapshot.Settings.WeeklyUsageFee.ToDecimal(), currency],
            ["Ganancia de colaboradores", snapshot.Settings.CollaboratorProfit.BasisPoints / 10000m, "Porcentaje"],
            ["Presupuesto mensual de insumos opcionales", snapshot.Settings.OptionalSuppliesMonthlyBudget.ToDecimal(), currency],
            ["Cantidad total de sillas", snapshot.Settings.TotalChairs, "Sillas"],
            ["Moneda configurada", SafeText(currency), "Código ISO"],
            ["Última actualización", snapshot.Settings.UpdatedUtc.ToLocalTime(), "Fecha y hora local"],
        ]);
        IXLWorksheet settingsSheet = workbook.Worksheet("Ajustes");
        settingsSheet.Cell(2, 2).Style.NumberFormat.Format = MoneyFormat;
        settingsSheet.Cell(3, 2).Style.NumberFormat.Format = "0.00%";
        settingsSheet.Cell(4, 2).Style.NumberFormat.Format = MoneyFormat;

        AddTable(workbook, "Uso del local", ["Nombre", "Fecha de ingreso", "Fecha de retiro", "Estado", "Deuda actual", "Moneda"],
            data.LocalUsePeople.OrderBy(x => x.Name).Select(person => (object?[])
            [SafeText(person.Name), Date(person.EntryDate), Date(person.ExitDate), person.IsCurrentOn(today) ? "Activo" : "Retirado",
             WeeklyChargeCalculator.CalculateDebt(data.WeeklyCharges.Where(x => x.PersonId == person.Id), data.LocalUsePayments.Where(x => x.PersonId == person.Id)).ToDecimal(), currency]), moneyColumns: [5]);

        AddTable(workbook, "Cuotas semanales", ["Persona", "Periodo inicial", "Periodo final", "Valor", "Moneda", "Estado"],
            data.WeeklyCharges.OrderBy(x => x.PeriodStart).Select(x => (object?[])
            [SafeText(PersonName(data, x.PersonId)), Date(x.PeriodStart), Date(x.PeriodEnd), x.Amount.ToDecimal(), currency,
             x.PeriodEnd < today ? "Vencida o causada" : "Programada"]), moneyColumns: [4]);

        AddTable(workbook, "Pagos por uso del local", ["Persona", "Fecha", "Valor", "Moneda", "Estado"],
            data.LocalUsePayments.OrderBy(x => x.PaymentDate).Select(x => (object?[])
            [SafeText(PersonName(data, x.PersonId)), Date(x.PaymentDate), x.Amount.ToDecimal(), currency, "Registrado"]), moneyColumns: [3]);

        AddTable(workbook, "Colaboradores", ["Nombre", "Fecha de inicio", "Fecha de retiro", "Estado"],
            data.Collaborators.OrderBy(x => x.Name).Select(x => (object?[])
            [SafeText(x.Name), Date(x.StartDate), Date(x.ExitDate), x.IsCurrentOn(today) ? "Activo" : "Retirado"]));

        AddTable(workbook, "Ventas", ["Producto", "Fecha", "Cantidad", "Valor de venta", "Costo estimado", "Moneda", "Estado"],
            data.InventoryMovements.Where(x => x.Type == InventoryMovementType.Sale).OrderBy(x => x.Date).Select(x => (object?[])
            [SafeText(ProductName(data, x.ProductId)), Date(x.Date), Math.Abs(x.QuantityDelta), x.CashAmount?.ToDecimal(), x.EstimatedCost?.ToDecimal(), currency, "Registrada"]), moneyColumns: [4, 5], quantityColumns: [3]);

        AddTable(workbook, "Productos", ["Nombre", "Categoría", "Unidad de medida", "Estado"],
            data.Products.OrderBy(x => x.Name).Select(x => (object?[])
            [SafeText(x.Name), SpanishText.For(x.Category), SafeText(x.UnitOfMeasure), "Activo"]));

        AddTable(workbook, "Inventario actual", ["Producto", "Categoría", "Unidad", "Cantidad actual", "Costo unitario promedio", "Valor estimado", "Moneda"],
            data.Products.OrderBy(x => x.Name).Select(product =>
            {
                InventoryMovement[] movements = data.InventoryMovements.Where(x => x.ProductId == product.Id).ToArray();
                decimal quantity = InventoryCalculator.CurrentQuantity(movements);
                decimal unitCost = InventoryCalculator.AverageUnitCost(movements).ToDecimal();
                return (object?[])[SafeText(product.Name), SpanishText.For(product.Category), SafeText(product.UnitOfMeasure), quantity, unitCost, quantity * unitCost, currency];
            }), moneyColumns: [5, 6], quantityColumns: [4]);

        AddTable(workbook, "Movimientos de inventario", ["Producto", "Fecha", "Tipo", "Variación de cantidad", "Movimiento de caja", "Costo estimado", "Moneda", "Estado"],
            data.InventoryMovements.OrderBy(x => x.Date).Select(x => (object?[])
            [SafeText(ProductName(data, x.ProductId)), Date(x.Date), MovementName(x.Type), x.QuantityDelta, x.CashAmount?.ToDecimal(), x.EstimatedCost?.ToDecimal(), currency, "Registrado"]), moneyColumns: [5, 6], quantityColumns: [4]);

        AddTable(workbook, "Planes de reposición", ["Producto", "Mes", "Cantidad necesaria", "Cantidad actual al cierre", "Compra sugerida", "Unidad", "Estado"],
            data.RestockPlans.OrderBy(x => x.Month.Year).ThenBy(x => x.Month.Month).Select(x =>
            {
                Product? product = data.Products.SingleOrDefault(p => p.Id == x.ProductId);
                decimal current = InventoryCalculator.CurrentQuantity(data.InventoryMovements.Where(m => m.ProductId == x.ProductId && m.Date <= x.Month.LastDay));
                return (object?[])[SafeText(product?.Name ?? "Producto eliminado"), Date(x.Month.FirstDay), x.NeededQuantity.Value, current, x.SuggestedPurchase(current), SafeText(product?.UnitOfMeasure ?? ""), x.Month.LastDay < today ? "Histórico" : "Planificado"];
            }), quantityColumns: [3, 4, 5]);

        AddFinancialSheet(workbook, "Otros ingresos", data, FinancialEntryType.OtherIncome, currency);
        AddFinancialSheet(workbook, "Gastos", data, FinancialEntryType.Expense, currency);
        AddFinancialSheet(workbook, "Imprevistos", data, FinancialEntryType.UnexpectedExpense, currency);

        AddTable(workbook, "Obligaciones", ["Nombre", "Tipo", "Fecha de vencimiento", "Valor esperado", "Valor pagado", "Saldo pendiente", "Moneda", "Recurrencia", "Estado"],
            data.Obligations.OrderBy(x => x.DueDate).Select(x =>
            {
                decimal paid = data.ObligationPayments.Where(p => p.ObligationId == x.Id).Sum(p => p.Amount.ToDecimal());
                return (object?[])[SafeText(x.Name), SpanishText.For(x.Type), Date(x.DueDate), x.ExpectedAmount.ToDecimal(), paid, Math.Max(0, x.ExpectedAmount.ToDecimal() - paid), currency, SpanishText.For(x.Recurrence), SpanishText.For(x.Status(data.ObligationPayments, today))];
            }), moneyColumns: [4, 5, 6]);

        AddTable(workbook, "Pagos de obligaciones", ["Obligación", "Fecha", "Valor", "Moneda", "Estado"],
            data.ObligationPayments.OrderBy(x => x.Date).Select(x => (object?[])
            [SafeText(ObligationName(data, x.ObligationId)), Date(x.Date), x.Amount.ToDecimal(), currency, "Registrado"]), moneyColumns: [3]);

        AddTable(workbook, "Mantenimiento", ["Equipo o bien", "Tipo de mantenimiento", "Fecha programada", "Costo estimado", "Fecha realizada", "Costo real", "Moneda", "Estado"],
            data.MaintenanceRecords.OrderBy(x => x.ScheduledDate).Select(x => (object?[])
            [SafeText(x.Asset), SafeText(x.MaintenanceType), Date(x.ScheduledDate), x.EstimatedCost?.ToDecimal(), Date(x.CompletedDate), x.ActualCost?.ToDecimal(), currency,
             x.CompletedDate.HasValue ? "Realizado" : x.ScheduledDate < today ? "Pendiente vencido" : "Programado"]), moneyColumns: [4, 6]);

        AddTable(workbook, "Cierres mensuales", ["Mes", "Porcentaje de colaboradores", "Ingresos", "Meta mensual", "Resultado base", "Fondo de colaboradores", "Resultado retenido", "Moneda", "Fecha de cierre", "Estado"],
            data.MonthlyCloses.OrderBy(x => x.Month.Year).ThenBy(x => x.Month.Month).Select(x => (object?[])
            [Date(x.Month.FirstDay), x.CollaboratorPercentageBasisPoints / 10000m, Minor(x.IncomeMinorUnits), Minor(x.GoalMinorUnits), Minor(x.BaseResultMinorUnits), Minor(x.FundMinorUnits), Minor(x.RetainedResultMinorUnits), currency, x.ClosedUtc.ToLocalTime(), x.IsConfirmed ? "Confirmado" : "Reabierto"]), moneyColumns: [3, 4, 5, 6, 7], percentColumns: [2]);

        AddTable(workbook, "Distribuciones a colaboradores", ["Mes", "Colaborador", "Valor asignado", "Valor pagado", "Saldo pendiente", "Moneda", "Estado"],
            data.MonthlyCloseParticipants.Select(x =>
            {
                MonthlyClose? close = data.MonthlyCloses.SingleOrDefault(c => c.Id == x.CloseId);
                decimal paid = data.DistributionPayments.Where(p => p.ParticipantId == x.Id).Sum(p => p.Amount.ToDecimal());
                return (object?[])[Date(close?.Month.FirstDay), SafeText(CollaboratorName(data, x.CollaboratorId)), x.Amount.ToDecimal(), paid, Math.Max(0, x.Amount.ToDecimal() - paid), currency, close?.IsConfirmed == true ? (paid >= x.Amount.ToDecimal() ? "Pagada" : "Pendiente") : "Cierre reabierto"];
            }), moneyColumns: [3, 4, 5]);

        AddTable(workbook, "Pagos a colaboradores", ["Mes", "Colaborador", "Fecha", "Valor", "Moneda", "Estado"],
            data.DistributionPayments.OrderBy(x => x.Date).Select(x =>
            {
                MonthlyCloseParticipant? participant = data.MonthlyCloseParticipants.SingleOrDefault(p => p.Id == x.ParticipantId);
                MonthlyClose? close = participant is null ? null : data.MonthlyCloses.SingleOrDefault(c => c.Id == participant.CloseId);
                return (object?[])[Date(close?.Month.FirstDay), SafeText(participant is null ? "Colaborador eliminado" : CollaboratorName(data, participant.CollaboratorId)), Date(x.Date), x.Amount.ToDecimal(), currency, "Registrado"];
            }), moneyColumns: [4]);

        AddMonthlySummaries(workbook, data, snapshot.Settings, currency, from, to);
        AddAnnualBalances(workbook, data, snapshot.Settings, currency, from, to);
        AddCashFlow(workbook, data, currency);

        AddTable(workbook, "Historial eliminado", ["Tipo de registro", "Descripción", "Fecha de eliminación", "Estado"],
            snapshot.Deleted.OrderBy(x => x.DeletedUtc).Select(x => (object?[])[x.Type, SafeText(x.Description), x.DeletedUtc?.ToLocalTime(), "Eliminado lógicamente"]));

        if (snapshot.Drafts.Count > 0)
        {
            AddTable(workbook, "Borradores sin finalizar", ["Módulo", "Formulario", "Clasificación", "Contenido técnico del borrador", "Última modificación"],
                snapshot.Drafts.Select(x => (object?[])[SafeText(x.Module), SafeText(x.FormType), x.Classification, SafeText(x.Payload), x.UpdatedUtc.ToLocalTime()]));
        }

        return workbook;
    }

    private static void AddSummary(XLWorkbook workbook, ExportSnapshot snapshot, DateTime cutoff, DateOnly today, string version, string currency, DateOnly? from, DateOnly? to)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add("Resumen general");
        sheet.Cell(1, 1).Value = "Exportación completa de Peluquería Admin";
        sheet.Range(1, 1, 1, 4).Merge().Style.Font.SetBold().Font.SetFontSize(16).Fill.SetBackgroundColor(XLColor.FromHtml("#172033")).Font.SetFontColor(XLColor.White);
        sheet.Cell(2, 1).Value = "Fecha y hora de exportación"; sheet.Cell(2, 2).Value = cutoff; sheet.Cell(2, 2).Style.DateFormat.Format = DateTimeFormat;
        sheet.Cell(3, 1).Value = "Versión del programa"; sheet.Cell(3, 2).Value = SafeText(version);
        sheet.Cell(4, 1).Value = "Moneda"; sheet.Cell(4, 2).Value = SafeText(currency);
        sheet.Cell(5, 1).Value = "Periodo total cubierto"; sheet.Cell(5, 2).Value = from.HasValue ? $"{from:yyyy-MM-dd} a {to:yyyy-MM-dd}" : "Sin operaciones fechadas";

        AdministrationData data = snapshot.Active;
        decimal localDebt = data.LocalUsePeople.Sum(person => WeeklyChargeCalculator.CalculateDebt(data.WeeklyCharges.Where(x => x.PersonId == person.Id), data.LocalUsePayments.Where(x => x.PersonId == person.Id)).ToDecimal());
        decimal inventory = data.Products.Sum(product =>
        {
            InventoryMovement[] movements = data.InventoryMovements.Where(x => x.ProductId == product.Id).ToArray();
            return InventoryCalculator.CurrentQuantity(movements) * InventoryCalculator.AverageUnitCost(movements).ToDecimal();
        });
        long cash = BuildCashMovements(data).Sum(x => x.SignedMinorUnits);
        object?[][] rows =
        [
            ["Personas con uso del local", data.LocalUsePeople.Count, "registros"],
            ["Deuda total por uso del local", localDebt, currency],
            ["Productos activos", data.Products.Count, "registros"],
            ["Valor estimado del inventario actual", inventory, currency],
            ["Obligaciones pendientes", data.Obligations.Sum(o => Math.Max(0, o.ExpectedAmount.ToDecimal() - data.ObligationPayments.Where(p => p.ObligationId == o.Id).Sum(p => p.Amount.ToDecimal()))), currency],
            ["Flujo de caja acumulado", Minor(cash), currency],
            ["Borradores sin finalizar", snapshot.Drafts.Count, "no registrados"],
            ["Registros eliminados lógicamente", snapshot.Deleted.Count, "historial"],
        ];
        WriteTable(sheet, 7, ["Indicador", "Valor", "Unidad o moneda"], rows);
        foreach (int row in new[] { 9, 11, 12, 13 }) sheet.Cell(row, 2).Style.NumberFormat.Format = MoneyFormat;
        sheet.SheetView.FreezeRows(1);
        FinalizeSheet(sheet);
    }

    private static void AddMonthlySummaries(XLWorkbook workbook, AdministrationData data, GeneralSettings settings, string currency, DateOnly? from, DateOnly? to)
    {
        if (!from.HasValue || !to.HasValue)
        {
            AddTable(workbook, "Resúmenes mensuales", ["Mes", "Ingresos", "Meta mensual", "Faltante", "Resultado base", "Fondo colaboradores", "Resultado retenido", "Moneda", "Origen"], []);
            return;
        }

        var months = MonthsBetween(from.Value, to.Value).Select(month =>
        {
            MonthlySummaryResult result = AdministrationReports.MonthlySummary(data, settings.OptionalSuppliesMonthlyBudget, settings.CollaboratorProfit, month);
            bool snapshot = data.MonthlyCloses.Any(x => x.Month == month && x.IsConfirmed);
            return (object?[])[Date(month.FirstDay), Minor(result.IncomeMinorUnits), Minor(result.GoalMinorUnits), Minor(result.MissingMinorUnits), Minor(result.BaseResultMinorUnits), Minor(result.CollaboratorFundMinorUnits), Minor(result.RetainedResultMinorUnits), currency, snapshot ? "Snapshot de cierre confirmado" : "Cálculo actual"];
        });
        AddTable(workbook, "Resúmenes mensuales", ["Mes", "Ingresos", "Meta mensual", "Faltante", "Resultado base", "Fondo colaboradores", "Resultado retenido", "Moneda", "Origen"], months, moneyColumns: [2, 3, 4, 5, 6, 7]);
    }

    private static void AddAnnualBalances(XLWorkbook workbook, AdministrationData data, GeneralSettings settings, string currency, DateOnly? from, DateOnly? to)
    {
        IEnumerable<int> years = from.HasValue && to.HasValue ? Enumerable.Range(from.Value.Year, to.Value.Year - from.Value.Year + 1) : [];
        var rows = years.Select(year =>
        {
            AnnualAdministrationReport report = AdministrationReports.Annual(data, settings.OptionalSuppliesMonthlyBudget, settings.CollaboratorProfit, year);
            MonthlyExpenseBreakdown e = report.Expenses;
            AnnualBalanceResult b = report.Balance;
            return (object?[])[year, Minor(b.IncomeMinorUnits), Minor(b.ExpenseMinorUnits), Minor(b.DistributionMinorUnits), Minor(b.RetainedMinorUnits), Minor(b.PendingMinorUnits), Minor(b.MissingMinorUnits), Minor(e.ServicesMinorUnits), Minor(e.TaxesMinorUnits), Minor(e.OtherObligationsMinorUnits), Minor(e.MerchandiseMinorUnits), Minor(e.MandatorySuppliesMinorUnits), Minor(e.OptionalSuppliesMinorUnits), Minor(e.MaintenanceMinorUnits), Minor(e.UnexpectedMinorUnits), Minor(e.OtherExpensesMinorUnits), Minor(e.PendingPlansMinorUnits), Minor(e.HistoricalAdjustmentMinorUnits), currency, report.Indicator];
        });
        AddTable(workbook, "Balance anual", ["Año", "Ingresos", "Meta y gastos", "Distribuciones pagadas", "Resultado retenido", "Pendientes", "Faltante", "Servicios", "Impuestos", "Otras obligaciones", "Mercancía", "Insumos obligatorios", "Insumos opcionales", "Mantenimiento", "Imprevistos", "Otros gastos", "Planes de reposición", "Ajuste histórico", "Moneda", "Indicador"], rows, moneyColumns: Enumerable.Range(2, 17).ToArray());
    }

    private static void AddCashFlow(XLWorkbook workbook, AdministrationData data, string currency)
    {
        long balance = 0;
        var rows = BuildCashMovements(data).OrderBy(x => x.Date).ThenBy(x => x.Category).Select(x =>
        {
            balance += x.SignedMinorUnits;
            return (object?[])[Date(x.Date), SafeText(x.Category), SafeText(x.Concept), Minor(x.SignedMinorUnits), Minor(balance), currency, x.SignedMinorUnits >= 0 ? "Entrada" : "Salida"];
        }).ToArray();
        AddTable(workbook, "Flujo de caja", ["Fecha", "Categoría", "Concepto", "Movimiento", "Saldo acumulado", "Moneda", "Tipo"], rows, moneyColumns: [4, 5]);
    }

    private static IReadOnlyList<CashMovement> BuildCashMovements(AdministrationData data)
    {
        var result = new List<CashMovement>();
        Guid[] confirmedCloseIds = data.MonthlyCloses.Where(x => x.IsConfirmed).Select(x => x.Id).ToArray();
        Guid[] participantIds = data.MonthlyCloseParticipants.Where(x => confirmedCloseIds.Contains(x.CloseId)).Select(x => x.Id).ToArray();
        result.AddRange(data.LocalUsePayments.Select(x => new CashMovement(x.PaymentDate, "Uso del local", $"Pago de {PersonName(data, x.PersonId)}", x.Amount.MinorUnits)));
        result.AddRange(data.InventoryMovements.Where(x => x.Type == InventoryMovementType.Sale).Select(x => new CashMovement(x.Date, "Ventas", ProductName(data, x.ProductId), x.CashAmount?.MinorUnits ?? 0)));
        result.AddRange(data.InventoryMovements.Where(x => x.Type == InventoryMovementType.Purchase).Select(x => new CashMovement(x.Date, "Compras", ProductName(data, x.ProductId), -(x.CashAmount?.MinorUnits ?? 0))));
        result.AddRange(data.FinancialEntries.Select(x => new CashMovement(x.Date, SpanishText.For(x.Type), x.Concept, x.Type == FinancialEntryType.OtherIncome ? x.Amount.MinorUnits : -x.Amount.MinorUnits)));
        result.AddRange(data.ObligationPayments.Select(x => new CashMovement(x.Date, "Obligaciones", ObligationName(data, x.ObligationId), -x.Amount.MinorUnits)));
        result.AddRange(data.MaintenanceRecords.Where(x => x.CompletedDate.HasValue && x.ActualCost.HasValue).Select(x => new CashMovement(x.CompletedDate!.Value, "Mantenimiento", x.Asset, -x.ActualCost!.Value.MinorUnits)));
        result.AddRange(data.DistributionPayments.Where(x => participantIds.Contains(x.ParticipantId)).Select(x => new CashMovement(x.Date, "Pagos a colaboradores", "Distribución pagada", -x.Amount.MinorUnits)));
        return result;
    }

    private static void AddFinancialSheet(XLWorkbook workbook, string name, AdministrationData data, FinancialEntryType type, string currency) =>
        AddTable(workbook, name, ["Fecha", "Concepto", "Categoría", "Valor", "Moneda", "Estado"],
            data.FinancialEntries.Where(x => x.Type == type).OrderBy(x => x.Date).Select(x => (object?[])
            [Date(x.Date), SafeText(x.Concept), x.Category.HasValue ? SpanishText.For(x.Category.Value) : "No aplica", x.Amount.ToDecimal(), currency, "Registrado"]), moneyColumns: [4]);

    private static void AddTable(XLWorkbook workbook, string name, string[] headers, IEnumerable<object?[]> rows, int[]? moneyColumns = null, int[]? percentColumns = null, int[]? quantityColumns = null)
    {
        IXLWorksheet sheet = workbook.Worksheets.Add(name);
        WriteTable(sheet, 1, headers, rows, moneyColumns, percentColumns, quantityColumns);
        sheet.SheetView.FreezeRows(1);
        FinalizeSheet(sheet);
    }

    private static void WriteTable(IXLWorksheet sheet, int headerRow, string[] headers, IEnumerable<object?[]> source, int[]? moneyColumns = null, int[]? percentColumns = null, int[]? quantityColumns = null)
    {
        object?[][] rows = source.ToArray();
        if (rows.Length > 0 && moneyColumns is { Length: > 0 })
        {
            var total = new object?[headers.Length];
            total[0] = "TOTAL";
            foreach (int column in moneyColumns)
            {
                total[column - 1] = rows.Sum(row => column <= row.Length && row[column - 1] is decimal value ? value : 0m);
            }
            rows = [.. rows, total];
        }
        for (int column = 0; column < headers.Length; column++)
        {
            sheet.Cell(headerRow, column + 1).Value = headers[column];
        }

        sheet.Range(headerRow, 1, headerRow, headers.Length).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#2E5BFF"));
        if (rows.Length == 0)
        {
            sheet.Cell(headerRow + 1, 1).Value = "Sin datos registrados";
            sheet.Range(headerRow, 1, headerRow + 1, headers.Length).SetAutoFilter();
        }
        else
        {
            for (int row = 0; row < rows.Length; row++)
            {
                for (int column = 0; column < headers.Length; column++)
                {
                    SetValue(sheet.Cell(headerRow + row + 1, column + 1), column < rows[row].Length ? rows[row][column] : null);
                }
            }

            IXLTable table = sheet.Range(headerRow, 1, headerRow + rows.Length, headers.Length).CreateTable($"Datos{Interlocked.Increment(ref tableSequence)}");
            table.Theme = XLTableTheme.TableStyleMedium2;
            table.ShowAutoFilter = true;
        }

        foreach (int column in moneyColumns ?? []) sheet.Column(column).Style.NumberFormat.Format = MoneyFormat;
        foreach (int column in percentColumns ?? []) sheet.Column(column).Style.NumberFormat.Format = "0.00%";
        foreach (int column in quantityColumns ?? []) sheet.Column(column).Style.NumberFormat.Format = QuantityFormat;
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = Blank.Value; break;
            case string text: cell.Value = SafeText(text); break;
            case DateTime dateTime: cell.Value = dateTime; cell.Style.DateFormat.Format = dateTime.TimeOfDay == TimeSpan.Zero ? DateFormat : DateTimeFormat; break;
            case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = DateFormat; break;
            case decimal number: cell.Value = number; break;
            case double number: cell.Value = number; break;
            case int number: cell.Value = number; break;
            case long number: cell.Value = number; break;
            default: cell.Value = SafeText(Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty); break;
        }
    }

    private static void FinalizeSheet(IXLWorksheet sheet)
    {
        sheet.ColumnsUsed().AdjustToContents(1, 60);
        foreach (IXLColumn column in sheet.ColumnsUsed())
        {
            column.Width = Math.Clamp(column.Width + 2, 10, 42);
        }
        sheet.RowsUsed().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static (DateOnly? From, DateOnly? To) CoveredPeriod(AdministrationData data)
    {
        var dates = new List<DateOnly>();
        dates.AddRange(data.LocalUsePeople.Select(x => x.EntryDate)); dates.AddRange(data.LocalUsePeople.Where(x => x.ExitDate.HasValue).Select(x => x.ExitDate!.Value));
        dates.AddRange(data.WeeklyRates.Select(x => x.EffectiveFrom)); dates.AddRange(data.WeeklyCharges.Select(x => x.PeriodStart)); dates.AddRange(data.LocalUsePayments.Select(x => x.PaymentDate));
        dates.AddRange(data.InventoryMovements.Select(x => x.Date)); dates.AddRange(data.RestockPlans.Select(x => x.Month.FirstDay)); dates.AddRange(data.FinancialEntries.Select(x => x.Date));
        dates.AddRange(data.Obligations.Select(x => x.DueDate)); dates.AddRange(data.ObligationPayments.Select(x => x.Date)); dates.AddRange(data.MaintenanceRecords.Select(x => x.ScheduledDate));
        dates.AddRange(data.MaintenanceRecords.Where(x => x.CompletedDate.HasValue).Select(x => x.CompletedDate!.Value)); dates.AddRange(data.Collaborators.Select(x => x.StartDate));
        dates.AddRange(data.MonthlyCloses.Select(x => x.Month.FirstDay)); dates.AddRange(data.DistributionPayments.Select(x => x.Date));
        return dates.Count == 0 ? (null, null) : (dates.Min(), dates.Max());
    }

    private static IEnumerable<YearMonth> MonthsBetween(DateOnly from, DateOnly to)
    {
        YearMonth current = YearMonth.From(from); YearMonth last = YearMonth.From(to);
        while (current.Year < last.Year || current.Year == last.Year && current.Month <= last.Month)
        {
            yield return current;
            current = current.Month == 12 ? new YearMonth(current.Year + 1, 1) : new YearMonth(current.Year, current.Month + 1);
        }
    }

    private static string UniquePath(string directory, string fileName)
    {
        string candidate = Path.Combine(directory, fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        for (int suffix = 2; File.Exists(candidate); suffix++) candidate = Path.Combine(directory, $"{stem}-{suffix}.xlsx");
        return candidate;
    }

    private static string ReadVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(ExcelExportService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? "Desconocida";
    }

    private static DeletedRecord Deleted(string type, string description, DateTime? deletedUtc) => new(type, description, deletedUtc);
    private static string SafeText(string value) => !string.IsNullOrEmpty(value) && "=+-@".Contains(value[0]) ? $"'{value}" : value;
    private static DateTime Date(DateOnly value) => value.ToDateTime(TimeOnly.MinValue);
    private static DateTime? Date(DateOnly? value) => value?.ToDateTime(TimeOnly.MinValue);
    private static DateTime? Date(DateOnly? value, bool _) => Date(value);
    private static decimal Minor(long value) => value / 100m;
    private static string PersonName(AdministrationData data, Guid id) => data.LocalUsePeople.SingleOrDefault(x => x.Id == id)?.Name ?? "Persona eliminada";
    private static string ProductName(AdministrationData data, Guid id) => data.Products.SingleOrDefault(x => x.Id == id)?.Name ?? "Producto eliminado";
    private static string ObligationName(AdministrationData data, Guid id) => data.Obligations.SingleOrDefault(x => x.Id == id)?.Name ?? "Obligación eliminada";
    private static string CollaboratorName(AdministrationData data, Guid id) => data.Collaborators.SingleOrDefault(x => x.Id == id)?.Name ?? "Colaborador eliminado";
    private static string MovementName(InventoryMovementType type) => type switch { InventoryMovementType.InitialStock => "Existencia inicial", InventoryMovementType.Purchase => "Compra", InventoryMovementType.Sale => "Venta", InventoryMovementType.InternalConsumption => "Consumo interno", InventoryMovementType.PhysicalCountAdjustment => "Ajuste por conteo físico", _ => type.ToString() };

    private sealed record ExportSnapshot(GeneralSettings Settings, AdministrationData Active, IReadOnlyList<DeletedRecord> Deleted, IReadOnlyList<DraftRecord> Drafts);
    private sealed record DeletedRecord(string Type, string Description, DateTime? DeletedUtc);
    private sealed record DraftRecord(string Module, string FormType, string Classification, string Payload, DateTime UpdatedUtc);
}
