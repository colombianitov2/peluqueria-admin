using System.Reflection;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Exporting;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Domain.Activity;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Notes;
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
        string destination = string.IsNullOrWhiteSpace(snapshot.Settings.ExportDirectory)
            ? desktopPath.GetDesktopPath()
            : snapshot.Settings.ExportDirectory;
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new InvalidOperationException("No hay una carpeta de exportación configurada.");
        }

        destination = Path.GetFullPath(destination);
        try
        {
            Directory.CreateDirectory(destination);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ArgumentException)
        {
            throw new InvalidOperationException($"No se puede crear la carpeta de exportación: {destination}", exception);
        }

        string finalPath = UniquePath(destination, $"PeluqueriaAdmin-{cutoffLocal:yyyy-MM-dd_HH-mm-ss}.xlsx");
        string temporaryPath = Path.Combine(destination, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp.xlsx");

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

        return new ExcelExportResult(finalPath, cutoffLocal, version, ApplicationCurrency.Code);
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
            await db.DistributionPayments.AsNoTracking().ToListAsync(cancellationToken),
            await db.Chairs.AsNoTracking().ToListAsync(cancellationToken),
            await db.ActivityRecords.AsNoTracking().ToListAsync(cancellationToken),
            await db.UnofficialExpenses.AsNoTracking().ToListAsync(cancellationToken),
            await db.CollaboratorContributions.AsNoTracking().ToListAsync(cancellationToken),
            await db.CollaboratorContributionEvents.AsNoTracking().ToListAsync(cancellationToken),
            await db.FinancialReserves.AsNoTracking().ToListAsync(cancellationToken),
            await db.FinancialCloseExclusions.AsNoTracking().ToListAsync(cancellationToken),
            await db.MonthlyPurchaseItems.AsNoTracking().ToListAsync(cancellationToken),
            await db.Loans.AsNoTracking().ToListAsync(cancellationToken),
            await db.LoanInstallments.AsNoTracking().ToListAsync(cancellationToken),
            await db.LoanPayments.AsNoTracking().ToListAsync(cancellationToken),
            await db.AnnualCloses.AsNoTracking().ToListAsync(cancellationToken),
            await db.AnnualCarryovers.AsNoTracking().ToListAsync(cancellationToken));

        var deletedPeople = await db.LocalUsePeople.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken);
        var deletedCollaborators = await db.Collaborators.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken);
        List<DeletedRecord> deleted = [];
        deleted.AddRange(deletedPeople.Select(x => Deleted("Persona que usa el local", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.WeeklyRates.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Tarifa semanal", x.Amount.ToDecimal().ToString("0.00"), x.DeletedUtc)));
        deleted.AddRange((await db.WeeklyCharges.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Cuota semanal", $"{x.PeriodStart:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.LocalUsePayments.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Pago por uso del local", $"{x.PaymentDate:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.Chairs.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Silla", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.UnofficialExpenses.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Gasto extraoficial", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.Products.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Producto", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.InventoryMovements.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Movimiento de inventario", $"{MovementName(x.Type)} · {x.QuantityDelta:0.###}", x.DeletedUtc)));
        deleted.AddRange((await db.FinancialEntries.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted(SpanishText.For(x.Type), x.Concept, x.DeletedUtc)));
        deleted.AddRange((await db.Obligations.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Obligación", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.ObligationPayments.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Pago de obligación", $"{x.Date:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.MaintenanceRecords.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Mantenimiento", $"{x.Asset} · {x.MaintenanceType}", x.DeletedUtc)));
        deleted.AddRange(deletedCollaborators.Select(x => Deleted("Colaborador", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.CollaboratorContributions.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Aporte de colaborador", $"{x.Date:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.MonthlyCloses.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Cierre mensual", x.Month.ToString(), x.DeletedUtc)));
        deleted.AddRange((await db.MonthlyCloseParticipants.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Distribución a colaborador", x.Amount.ToDecimal().ToString("0.00"), x.DeletedUtc)));
        deleted.AddRange((await db.DistributionPayments.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Pago a colaborador", $"{x.Date:yyyy-MM-dd} · {x.Amount.ToDecimal():0.00}", x.DeletedUtc)));
        deleted.AddRange((await db.FinancialReserves.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Reserva financiera", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.FinancialCloseExclusions.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Exclusión de cierre", x.Reason, x.DeletedUtc)));
        deleted.AddRange((await db.MonthlyPurchaseItems.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Artículo de compra mensual", x.Month.ToString(), x.DeletedUtc)));
        deleted.AddRange((await db.Loans.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Préstamo", x.Name, x.DeletedUtc)));
        deleted.AddRange((await db.LoanPayments.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedUtc != null).ToListAsync(cancellationToken)).Select(x => Deleted("Cuota de préstamo", x.Date.ToString("yyyy-MM-dd"), x.DeletedUtc)));

        var drafts = await db.FormDrafts.AsNoTracking().OrderBy(x => x.UpdatedUtc).ToListAsync(cancellationToken);
        AppNote? note = await db.Notes.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ExportSnapshot(
            settings,
            active,
            deleted,
            drafts.Select(x => new DraftRecord(x.Module, x.FormType, x.IsEdit ? "Edición no finalizada" : "Nuevo registro no finalizado", x.PayloadJson, x.UpdatedUtc)).ToArray(),
            note?.Content,
            note?.UpdatedUtc,
            deletedPeople.ToDictionary(x => x.Id, x => x.Name),
            deletedCollaborators.ToDictionary(x => x.Id, x => x.Name));
    }

    private static XLWorkbook BuildWorkbook(ExportSnapshot snapshot, DateTime cutoff, DateOnly today, string version)
    {
        tableSequence = 0;
        var workbook = new XLWorkbook();
        workbook.Properties.Title = "Exportación completa de Peluquería Admin";
        workbook.Properties.Subject = $"Fotografía de datos al {cutoff:yyyy-MM-dd HH:mm:ss}";
        workbook.Properties.Author = "Peluquería Admin";

        AdministrationData data = snapshot.Active;
        string currency = ApplicationCurrency.Code;
        (DateOnly? from, DateOnly? to) = CoveredPeriod(data);

        AddSummary(workbook, snapshot, cutoff, today, version, currency, from, to);
        AddTable(workbook, "Ajustes", ["Ajuste", "Valor", "Unidad"],
        [
            ["Valor semanal general por uso del local", snapshot.Settings.WeeklyUsageFee.ToDecimal(), currency],
            ["Ganancia de colaboradores", snapshot.Settings.CollaboratorProfit.BasisPoints / 10000m, "Porcentaje"],
            ["Moneda única", SafeText(currency), "Código ISO"],
            ["Carpeta de exportación", SafeText(snapshot.Settings.ExportDirectory), "Ruta local"],
            ["Última actualización", snapshot.Settings.UpdatedUtc.ToLocalTime(), "Fecha y hora local"],
        ]);
        IXLWorksheet settingsSheet = workbook.Worksheet("Ajustes");
        settingsSheet.Cell(2, 2).Style.NumberFormat.Format = MoneyFormat;
        settingsSheet.Cell(3, 2).Style.NumberFormat.Format = "0.00%";

        AddTable(workbook, "Notas", ["Contenido", "Última actualización"],
            string.IsNullOrEmpty(snapshot.NoteContent)
                ? []
                : [(object?[])[SafeText(snapshot.NoteContent), snapshot.NoteUpdatedUtc?.ToLocalTime()]]);

        SuggestedChairPrice suggested = SuggestedChairPriceCalculator.Calculate(
            data, snapshot.Settings.WeeklyUsageFee,
            YearMonth.From(today), today);
        AddTable(workbook, "Precio sugerido por silla", ["Concepto", "Valor", "Moneda", "Explicación"],
        [
            ["Precio semanal actual", Minor(suggested.CurrentWeeklyMinorUnits), currency, "Tarifa configurada"],
            ["Equivalente mensual actual", Minor(suggested.CurrentMonthlyEquivalentMinorUnits), currency, "Tarifa semanal × 52 / 12"],
            ["Precio semanal sugerido", Minor(suggested.SuggestedWeeklyPerChairMinorUnits), currency, suggested.Explanation],
            ["Precio mensual sugerido", Minor(suggested.SuggestedMonthlyPerChairMinorUnits), currency, suggested.Explanation],
            ["Meta mensual oficial", Minor(suggested.OfficialGoalMinorUnits), currency, "Balance oficial del mes"],
            ["Gastos extraoficiales vigentes", Minor(suggested.UnofficialExpensesMinorUnits), currency, "Separados del Balance anual oficial"],
            ["Ventas y otros ingresos no provenientes de sillas", Minor(suggested.ExpectedNonChairIncomeMinorUnits), currency, "Se restan del monto por cubrir"],
            ["Monto mensual por cubrir", Minor(suggested.AmountToCoverMinorUnits), currency, $"Calculado entre {suggested.OccupiedChairs} sillas ocupadas"],
        ], moneyColumns: [2]);

        AddTable(workbook, "Uso del local", ["Nombre", "Fecha de ingreso", "Fecha de retiro", "Silla asignada", "Descripción", "Estado", "Deuda actual", "Saldo a favor", "Próximo cobro de cuota", "Próximo pago requerido", "Cobertura estimada hasta", "Moneda"],
            data.LocalUsePeople.OrderBy(x => x.Name).Select(person =>
            {
                WorkerAccountBalance balance = WeeklyChargeCalculator.CalculateAccount(
                    person,
                    data.WeeklyCharges.Where(x => x.PersonId == person.Id),
                    data.LocalUsePayments.Where(x => x.PersonId == person.Id),
                    data.WeeklyRates,
                    today);
                return (object?[])
                [
                    SafeText(person.Name), Date(person.EntryDate), Date(person.ExitDate),
                    SafeText(data.Chairs.SingleOrDefault(x => x.AssignedPersonId == person.Id)?.Name ?? "Sin silla"),
                    SafeText(person.Description ?? ""), person.IsCurrentOn(today) ? "Activo" : "Retirado",
                    balance.Debt.ToDecimal(), balance.Credit.ToDecimal(), Date(balance.NextChargeDate),
                    Date(balance.NextRequiredPaymentDate), Date(balance.CoveredThroughDate), currency,
                ];
            }), moneyColumns: [7, 8]);

        IEnumerable<object?[]> workerHistory = data.LocalUsePeople.SelectMany(person =>
            data.WeeklyCharges.Where(x => x.PersonId == person.Id).Select(x => (object?[])
            [Date(x.PeriodEnd), SafeText(person.Name), "Cuota semanal", SafeText($"Periodo {x.PeriodStart:yyyy-MM-dd} a {x.PeriodEnd:yyyy-MM-dd}; sábado habitual {x.DueDate:yyyy-MM-dd}"), x.Amount.ToDecimal(), currency])
            .Concat(data.LocalUsePayments.Where(x => x.PersonId == person.Id).Select(x => (object?[])
            [Date(x.PaymentDate), SafeText(person.Name), "Pago", SafeText(x.Description ?? ""), x.Amount.ToDecimal(), currency])))
            .Concat(data.ActivityRecords.Where(x => x.Module == "Uso del local" && x.EntityId.HasValue
                && data.LocalUsePeople.Any(person => person.Id == x.EntityId.Value)).Select(x => (object?[])
            [Date(x.ActivityDate), SafeText(PersonName(data, snapshot.DeletedPeople, x.EntityId!.Value)), SafeText(x.Action), SafeText(x.Summary), null, currency]));
        AddTable(workbook, "Historial trabajadores", ["Fecha", "Trabajador", "Operación", "Detalle", "Valor", "Moneda"],
            workerHistory.OrderBy(row => row[0]), moneyColumns: [5]);

        AddTable(workbook, "Sillas", ["Nombre", "Fecha de creación", "Trabajador asignado", "Descripción", "Estado"],
            data.Chairs.OrderBy(x => x.Name).Select(x => (object?[])
            [SafeText(x.Name), Date(x.CreationDate), SafeText(x.AssignedPersonId.HasValue ? PersonName(data, snapshot.DeletedPeople, x.AssignedPersonId.Value) : "Vacía"), SafeText(x.Description ?? ""), x.AssignedPersonId.HasValue ? "Ocupada" : "Vacía"]));

        AddTable(workbook, "Asignaciones actuales", ["Silla", "Trabajador", "Fecha de ingreso", "Estado"],
            data.Chairs.Where(x => x.AssignedPersonId.HasValue).OrderBy(x => x.Name).Select(x =>
            {
                LocalUsePerson? person = data.LocalUsePeople.SingleOrDefault(p => p.Id == x.AssignedPersonId);
                return (object?[])[SafeText(x.Name), SafeText(person?.Name ?? "Persona eliminada"), Date(person?.EntryDate), person?.IsCurrentOn(today) == true ? "Vigente" : "Asignación por revisar"];
            }));

        AddTable(workbook, "Cuotas semanales", ["Persona", "Periodo inicial", "Periodo final", "Fecha de vencimiento", "Valor", "Moneda", "Estado"],
            data.WeeklyCharges.OrderBy(x => x.PeriodStart).Select(x => (object?[])
            [SafeText(PersonName(data, snapshot.DeletedPeople, x.PersonId)), Date(x.PeriodStart), Date(x.PeriodEnd), Date(x.DueDate), x.Amount.ToDecimal(), currency,
             x.DueDate <= today ? "Causada" : "Futura"]), moneyColumns: [5]);

        AddTable(workbook, "Pagos por uso del local", ["Persona", "Fecha", "Valor", "Moneda", "Descripción", "Estado"],
            data.LocalUsePayments.OrderBy(x => x.PaymentDate).Select(x => (object?[])
            [SafeText(PersonName(data, snapshot.DeletedPeople, x.PersonId)), Date(x.PaymentDate), x.Amount.ToDecimal(), currency, SafeText(x.Description ?? ""), "Registrado"]), moneyColumns: [3]);

        AddTable(workbook, "Cuentas por cobrar", ["Persona", "Cuotas causadas", "Pagos recibidos", "Saldo por cobrar", "Moneda", "Estado"],
            data.LocalUsePeople.OrderBy(x => x.Name).Select(person =>
            {
                decimal charges = data.WeeklyCharges.Where(x => x.PersonId == person.Id && x.PeriodEnd <= today).Sum(x => x.Amount.ToDecimal());
                decimal payments = data.LocalUsePayments.Where(x => x.PersonId == person.Id && x.PaymentDate <= today).Sum(x => x.Amount.ToDecimal());
                decimal pending = Math.Max(0, charges - payments);
                return (object?[])[SafeText(person.Name), charges, payments, pending, currency, pending > 0 ? "Pendiente" : "Al día"];
            }), moneyColumns: [2, 3, 4]);

        AddTable(workbook, "Colaboradores", ["Nombre", "Fecha de inicio", "Fecha de retiro", "Porcentaje de ganancia", "Total aportado vigente", "Moneda", "Descripción", "Estado"],
            data.Collaborators.OrderBy(x => x.Name).Select(x => (object?[])
            [SafeText(x.Name), Date(x.StartDate), Date(x.ExitDate), x.FundParticipationBasisPoints / 10_000m,
             data.CollaboratorContributions.Where(item => item.CollaboratorId == x.Id).Sum(item => item.Amount.ToDecimal()),
             currency, SafeText(x.Description ?? ""), x.IsCurrentOn(today) ? "Activo" : "Retirado"]),
            moneyColumns: [5], percentColumns: [4]);

        AddTable(workbook, "Aportes colaboradores", ["Fecha", "Colaborador", "Valor", "Moneda", "Descripción", "Clasificación", "Estado"],
            data.CollaboratorContributions.OrderBy(x => x.Date).ThenBy(x => x.CreatedUtc).Select(x => (object?[])
            [Date(x.Date), SafeText(CollaboratorName(data, snapshot.DeletedCollaborators, x.CollaboratorId)), x.Amount.ToDecimal(), currency, SafeText(x.Description ?? ""), "Capital / inversión no operativa", "Registrado"]), moneyColumns: [3]);

        IEnumerable<object?[]> collaboratorHistory = data.CollaboratorContributionEvents.Select(x => (object?[])
            [Date(x.EffectiveDate), SafeText(CollaboratorName(data, snapshot.DeletedCollaborators, x.CollaboratorId)),
             x.EventType switch
             {
                 CollaboratorContributionEventType.Created => "Aporte original",
                 CollaboratorContributionEventType.Edited => "Aporte editado",
                 CollaboratorContributionEventType.Deleted => "Aporte eliminado",
                 _ => "Aporte histórico migrado",
             },
             SafeText(x.EventType == CollaboratorContributionEventType.Edited
                 ? $"Anterior {x.PreviousAmount?.ToDecimal():N2}; nuevo {x.Amount.ToDecimal():N2}. {x.Description}"
                 : x.Description ?? ""),
             x.Amount.ToDecimal(), currency])
            .Concat(data.MonthlyCloseParticipants.Select(x =>
            {
                MonthlyClose? close = data.MonthlyCloses.SingleOrDefault(c => c.Id == x.CloseId);
                return (object?[])[Date(close?.Month.LastDay), SafeText(CollaboratorName(data, snapshot.DeletedCollaborators, x.CollaboratorId)), "Participación de cierre", SafeText(close?.Month.ToString() ?? ""), x.Amount.ToDecimal(), currency];
            }));
        AddTable(workbook, "Historial colaboradores", ["Fecha", "Colaborador", "Operación", "Detalle", "Valor", "Moneda"],
            collaboratorHistory.OrderBy(row => row[0]), moneyColumns: [5]);

        AddTable(workbook, "Ventas", ["Producto", "Fecha", "Cantidad", "Valor de venta", "Costo estimado", "Moneda", "Descripción", "Estado"],
            data.InventoryMovements.Where(x => x.Type == InventoryMovementType.Sale).OrderBy(x => x.Date).Select(x => (object?[])
            [SafeText(ProductName(data, x.ProductId)), Date(x.Date), Math.Abs(x.QuantityDelta), x.CashAmount?.ToDecimal(), x.EstimatedCost?.ToDecimal(), currency, SafeText(x.Description ?? ""), "Registrada"]), moneyColumns: [4, 5], quantityColumns: [3]);

        AddTable(workbook, "Productos", ["Nombre", "Categoría", "Costo configurado", "Precio predeterminado de venta", "Moneda", "Descripción", "Disponible para venta", "Estado"],
            data.Products.OrderBy(x => x.Name).Select(x => (object?[])
            [SafeText(x.Name), SpanishText.For(x.Category), x.DefaultUnitCost?.ToDecimal(), x.DefaultSalePrice?.ToDecimal(), currency, SafeText(x.Description ?? ""), x.IsForSale ? "Sí" : "No", "Activo"]), moneyColumns: [3, 4]);

        AddTable(workbook, "Inventario actual", ["Producto", "Categoría", "Cantidad actual", "Costo unitario promedio", "Valor estimado", "Moneda"],
            data.Products.OrderBy(x => x.Name).Select(product =>
            {
                InventoryMovement[] movements = data.InventoryMovements.Where(x => x.ProductId == product.Id).ToArray();
                decimal quantity = InventoryCalculator.CurrentQuantity(movements);
                decimal unitCost = InventoryCalculator.AverageUnitCost(movements).ToDecimal();
                return (object?[])[SafeText(product.Name), SpanishText.For(product.Category), quantity, unitCost, quantity * unitCost, currency];
            }), moneyColumns: [4, 5], quantityColumns: [3]);

        AddTable(workbook, "Movimientos de inventario", ["Producto", "Fecha", "Tipo", "Variación de cantidad", "Movimiento de caja", "Costo estimado", "Moneda", "Descripción", "Estado"],
            data.InventoryMovements.OrderBy(x => x.Date).Select(x => (object?[])
            [SafeText(ProductName(data, x.ProductId)), Date(x.Date), MovementName(x.Type), x.QuantityDelta, x.CashAmount?.ToDecimal(), x.EstimatedCost?.ToDecimal(), currency, SafeText(x.Description ?? ""), "Registrado"]), moneyColumns: [5, 6], quantityColumns: [4]);

        AddTable(workbook, "Lista mensual de compra", ["Mes", "Producto", "Categoría", "Cantidad", "Costo esperado unitario", "Total esperado", "Moneda", "Activa", "Reserva cuando llega a cero", "Compra vinculada", "Descripción"],
            data.MonthlyPurchaseItems.OrderBy(x => x.Month.Year).ThenBy(x => x.Month.Month).Select(x =>
            {
                Product? product = x.ProductId.HasValue
                    ? data.Products.SingleOrDefault(p => p.Id == x.ProductId.Value)
                    : null;
                string linkState = x.PurchaseMovementId.HasValue
                    ? $"Sí · {SafeText(product?.Name ?? x.Name)}"
                    : x.ProductId.HasValue
                        ? $"Producto vinculado · {SafeText(product?.Name ?? "eliminado")}"
                        : "Sin vínculo de inventario";
                return (object?[])[Date(x.Month.FirstDay), SafeText(x.Name), SpanishText.For(x.Category), x.Quantity,
                    x.ExpectedUnitCost.ToDecimal(), Minor(x.ExpectedTotalMinorUnits), currency, x.IsActive ? "Sí" : "No", x.ReserveWhenOutOfStock ? "Sí" : "No",
                    linkState, SafeText(x.Description ?? "")];
            }), moneyColumns: [5, 6], quantityColumns: [4]);

        AddFinancialSheet(workbook, "Otros ingresos", data, FinancialEntryType.OtherIncome, currency);
        AddFinancialSheet(workbook, "Gastos", data, FinancialEntryType.Expense, currency);
        AddFinancialSheet(workbook, "Imprevistos", data, FinancialEntryType.UnexpectedExpense, currency);

        AddTable(workbook, "Gastos extraoficiales", ["Nombre", "Valor mensual", "Moneda", "Vigente desde", "Descripción", "Estado"],
            data.UnofficialExpenses.OrderBy(x => x.EffectiveFrom).Select(x => (object?[])
            [SafeText(x.Name), x.MonthlyAmount.ToDecimal(), currency, Date(x.EffectiveFrom), SafeText(x.Description ?? ""), x.AppliesOn(today) ? "Vigente" : "Futuro"]), moneyColumns: [2]);

        AddTable(workbook, "Obligaciones", ["Nombre", "Tipo", "Fecha de vencimiento", "Valor esperado", "Valor pagado", "Saldo pendiente", "Moneda", "Recurrencia", "Descripción", "Estado"],
            data.Obligations.OrderBy(x => x.DueDate).Select(x =>
            {
                decimal paid = data.ObligationPayments.Where(p => p.ObligationId == x.Id).Sum(p => p.Amount.ToDecimal());
                return (object?[])[SafeText(x.Name), SpanishText.For(x.Type), Date(x.DueDate), x.ExpectedAmount.ToDecimal(), paid, Math.Max(0, x.ExpectedAmount.ToDecimal() - paid), currency, SpanishText.For(x.Recurrence), SafeText(x.Description ?? ""), SpanishText.For(x.Status(data.ObligationPayments, today))];
            }), moneyColumns: [4, 5, 6]);

        AddTable(workbook, "Pagos de obligaciones", ["Obligación", "Fecha", "Valor", "Moneda", "Descripción", "Estado"],
            data.ObligationPayments.OrderBy(x => x.Date).Select(x => (object?[])
            [SafeText(ObligationName(data, x.ObligationId)), Date(x.Date), x.Amount.ToDecimal(), currency, SafeText(x.Description ?? ""), "Registrado"]), moneyColumns: [3]);

        IEnumerable<object?[]> payableRows = data.Obligations.Select(x =>
        {
            decimal paid = data.ObligationPayments.Where(p => p.ObligationId == x.Id).Sum(p => p.Amount.ToDecimal());
            return (object?[])["Obligación", SafeText(x.Name), Date(x.DueDate), Math.Max(0, x.ExpectedAmount.ToDecimal() - paid), currency, paid >= x.ExpectedAmount.ToDecimal() ? "Pagada" : "Pendiente"];
        }).Where(row => row[3] is decimal value && value > 0)
        .Concat(data.MaintenanceRecords.Where(x => !x.CompletedDate.HasValue).Select(x => (object?[])["Mantenimiento", SafeText($"{x.Asset}: {x.MaintenanceType}"), Date(x.ScheduledDate), x.EstimatedCost?.ToDecimal(), currency, "Pendiente"]))
        .Concat(data.LoanInstallments
            .Where(x => data.LoanPayments.All(payment => payment.InstallmentId != x.Id))
            .Select(x => (object?[])["Cuota de préstamo",
                SafeText(data.Loans.SingleOrDefault(loan => loan.Id == x.LoanId)?.Name ?? "Préstamo eliminado"),
                Date(x.DueDate), x.Amount.ToDecimal(), currency, x.DueDate < today ? "Vencida" : "Pendiente"]));
        AddTable(workbook, "Cuentas por pagar", ["Origen", "Nombre", "Fecha exigible", "Valor pendiente", "Moneda", "Estado"], payableRows, moneyColumns: [4]);

        AddTable(workbook, "Préstamos", ["Nombre", "Método", "Capital recibido", "Total esperado", "Interés total", "Interés mensual", "Tasa mensual equivalente", "Saldo pendiente", "Cuota habitual", "Moneda", "Primera cuota", "Número de cuotas", "Próximo vencimiento", "Descripción", "Estado"],
            data.Loans.OrderBy(x => x.StartDate).Select(x => (object?[])[
                SafeText(x.Name),
                x.CalculationMethod switch
                {
                    LoanCalculationMethod.MonthlyBalanceInterest => "Interés mensual sobre saldo",
                    LoanCalculationMethod.AgreedFinalAmount => "Cantidad final acordada",
                    _ => "Préstamo anterior",
                },
                x.InitialBalance.ToDecimal(), x.ExpectedTotal.ToDecimal(), x.TotalInterest.ToDecimal(),
                x.MonthlyInterestBasisPoints / 10_000m, x.EquivalentMonthlyRateBasisPoints / 10_000m,
                x.PendingBalance.ToDecimal(), x.UsualInstallment.ToDecimal(), currency,
                Date(x.StartDate), x.InstallmentCount, Date(x.NextDueDate), SafeText(x.Description ?? ""),
                x.IsPaid ? "Pagado" : "Pendiente"]), moneyColumns: [3, 4, 5, 8, 9], percentColumns: [6, 7]);
        AddTable(workbook, "Cuotas de préstamos", ["Préstamo", "Número", "Vencimiento", "Importe", "Capital", "Interés", "Saldo de capital posterior", "Moneda", "Descripción", "Estado"],
            data.LoanInstallments.OrderBy(x => x.DueDate).ThenBy(x => x.Number).Select(x => (object?[])[
                SafeText(data.Loans.SingleOrDefault(l => l.Id == x.LoanId)?.Name ?? "Préstamo eliminado"),
                x.Number, Date(x.DueDate), x.Amount.ToDecimal(), x.Principal.ToDecimal(), x.Interest.ToDecimal(),
                x.PrincipalBalanceAfter.ToDecimal(), currency, SafeText(x.Description ?? ""),
                data.LoanPayments.Any(payment => payment.InstallmentId == x.Id)
                    ? "Pagada"
                    : x.DueDate < today ? "Vencida" : "Pendiente"]), moneyColumns: [4, 5, 6, 7]);
        AddTable(workbook, "Pagos de préstamos", ["Préstamo", "Cuota", "Fecha", "Valor", "Moneda", "Descripción", "Estado"],
            data.LoanPayments.OrderBy(x => x.Date).Select(x => (object?[])[SafeText(data.Loans.SingleOrDefault(l => l.Id == x.LoanId)?.Name ?? "Préstamo eliminado"),
                x.InstallmentId.HasValue
                    ? data.LoanInstallments.SingleOrDefault(i => i.Id == x.InstallmentId.Value)?.Number
                    : null,
                Date(x.Date), x.Amount.ToDecimal(), currency, SafeText(x.Description ?? ""), "Registrado"]), moneyColumns: [4]);

        AddTable(workbook, "Mantenimiento", ["Equipo o bien", "Tipo de mantenimiento", "Fecha programada", "Costo estimado", "Fecha realizada", "Costo real", "Moneda", "Frecuencia", "Intervalo personalizado", "Unidad del intervalo", "Número de ocurrencia", "Descripción", "Estado"],
            data.MaintenanceRecords.OrderBy(x => x.ScheduledDate).Select(x => (object?[])
            [SafeText(x.Asset), SafeText(x.MaintenanceType), Date(x.ScheduledDate), x.EstimatedCost?.ToDecimal(), Date(x.CompletedDate), x.ActualCost?.ToDecimal(), currency, MaintenanceFrequencyName(x.Frequency), x.CustomInterval, MaintenanceUnitName(x.CustomIntervalUnit), x.OccurrenceNumber + 1, SafeText(x.Description ?? ""),
             x.CompletedDate.HasValue ? "Realizado" : x.ScheduledDate < today ? "Pendiente vencido" : "Programado"]), moneyColumns: [4, 6]);

        AddTable(workbook, "Cierres mensuales", ["Mes", "Porcentaje global", "Ingresos cobrados", "Cuentas por cobrar", "Egresos pagados", "Cuentas por pagar", "Reservas nuevas", "Reservas arrastradas", "Ajustes de reservas", "Pagos de préstamos", "Financiación recibida", "Compromisos anteriores", "Resultado distribuible", "Punto de equilibrio", "Faltante", "Fondo de colaboradores", "Retenido por el local", "Moneda", "Fecha de cierre", "Descripción", "Estado"],
            data.MonthlyCloses.OrderBy(x => x.Month.Year).ThenBy(x => x.Month.Month).Select(x => (object?[])
            [Date(x.Month.FirstDay), x.CollaboratorPercentageBasisPoints / 10000m, Minor(x.IncomeMinorUnits), Minor(x.AccountsReceivableMinorUnits), Minor(x.PaidOutflowsMinorUnits), Minor(x.AccountsPayableMinorUnits), Minor(x.NewReservesMinorUnits), Minor(x.CarriedReservesMinorUnits), Minor(x.ReserveAdjustmentsMinorUnits), Minor(x.LoanPaymentsMinorUnits), Minor(x.FinancingReceivedMinorUnits), Minor(x.PriorUncoveredCommitmentsMinorUnits), Minor(x.BaseResultMinorUnits), Minor(x.BreakEvenMinorUnits), Minor(x.ShortfallMinorUnits), Minor(x.FundMinorUnits), Minor(x.RetainedResultMinorUnits), currency, x.ClosedUtc.ToLocalTime(), SafeText(x.Description ?? ""), x.IsConfirmed ? "Confirmado" : "Reabierto"]), moneyColumns: Enumerable.Range(3, 15).ToArray(), percentColumns: [2]);

        AddTable(workbook, "Reservas financieras", ["Mes de creación", "Origen", "Nombre", "Fecha exigible", "Valor reservado", "Valor real", "Moneda", "Fecha de consumo", "Estado"],
            data.FinancialReserves.OrderBy(x => x.DueDate).Select(x => (object?[])[Date(x.Month.FirstDay), CommitmentSourceName(x.SourceType), SafeText(x.Name), Date(x.DueDate),
                x.ReservedAmount.ToDecimal(), x.ActualAmount?.ToDecimal(), currency, Date(x.SettledDate), x.IsConsumed ? "Consumida" : "Comprometida"]), moneyColumns: [5, 6]);
        AddTable(workbook, "Exclusiones de cierre", ["Mes", "Origen", "Motivo", "Estado"],
            data.FinancialCloseExclusions.OrderBy(x => x.Month.Year).ThenBy(x => x.Month.Month).Select(x => (object?[])[Date(x.Month.FirstDay), CommitmentSourceName(x.SourceType), SafeText(x.Reason), "Excluida y auditada"]));

        AddTable(workbook, "Distribuciones a colaboradores", ["Mes", "Colaborador", "Porcentaje global congelado", "Porcentaje individual congelado", "Valor asignado", "Valor pagado", "Saldo pendiente", "Moneda", "Estado"],
            data.MonthlyCloseParticipants.Select(x =>
            {
                MonthlyClose? close = data.MonthlyCloses.SingleOrDefault(c => c.Id == x.CloseId);
                decimal paid = data.DistributionPayments.Where(p => p.ParticipantId == x.Id).Sum(p => p.Amount.ToDecimal());
                return (object?[])[Date(close?.Month.FirstDay), SafeText(CollaboratorName(data, snapshot.DeletedCollaborators, x.CollaboratorId)), x.GlobalPercentageBasisPoints / 10_000m, x.IndividualPercentageBasisPoints / 10_000m, x.Amount.ToDecimal(), paid, Math.Max(0, x.Amount.ToDecimal() - paid), currency, close?.IsConfirmed == true ? (paid >= x.Amount.ToDecimal() ? "Pagada completa" : "Pendiente de pago completo") : "Cierre reabierto"];
            }), moneyColumns: [5, 6, 7], percentColumns: [3, 4]);

        AddTable(workbook, "Pagos a colaboradores", ["Mes", "Colaborador", "Fecha", "Valor", "Moneda", "Descripción", "Estado"],
            data.DistributionPayments.OrderBy(x => x.Date).Select(x =>
            {
                MonthlyCloseParticipant? participant = data.MonthlyCloseParticipants.SingleOrDefault(p => p.Id == x.ParticipantId);
                MonthlyClose? close = participant is null ? null : data.MonthlyCloses.SingleOrDefault(c => c.Id == participant.CloseId);
                return (object?[])[Date(close?.Month.FirstDay), SafeText(participant is null ? "Colaborador eliminado" : CollaboratorName(data, snapshot.DeletedCollaborators, participant.CollaboratorId)), Date(x.Date), x.Amount.ToDecimal(), currency, SafeText(x.Description ?? ""), "Registrado"];
            }), moneyColumns: [4]);

        AddTable(workbook, "Historial fin. colaboradores", ["Mes", "Colaborador", "Participación asignada", "Pagado", "Pendiente", "Moneda", "Estado"],
            data.MonthlyCloseParticipants.Select(x =>
            {
                MonthlyClose? close = data.MonthlyCloses.SingleOrDefault(c => c.Id == x.CloseId);
                decimal paid = data.DistributionPayments.Where(p => p.ParticipantId == x.Id).Sum(p => p.Amount.ToDecimal());
                return (object?[])[Date(close?.Month.FirstDay), SafeText(CollaboratorName(data, snapshot.DeletedCollaborators, x.CollaboratorId)), x.Amount.ToDecimal(), paid, Math.Max(0, x.Amount.ToDecimal() - paid), currency, close?.IsConfirmed == true ? "Cierre confirmado" : "Cierre reabierto"];
            }), moneyColumns: [3, 4, 5]);

        AddMonthlySummaries(workbook, data, snapshot.Settings, currency, from, to);
        AddAnnualBalances(workbook, data, snapshot.Settings, currency, from, to);
        AddTable(workbook, "Cierres anuales", ["Año", "Ingresos cobrados", "Egresos pagados", "Reservas", "Obligaciones", "Pagos de préstamos", "Fondo de colaboradores", "Resultado", "Cuentas por cobrar", "Cuentas por pagar", "Reservas pendientes", "Préstamos pendientes", "Superávit", "Déficit", "Saldo disponible", "Saldo proyectado siguiente año", "Moneda", "Fecha de cierre", "Estado"],
            data.AnnualCloses.OrderBy(x => x.Year).Select(x => (object?[])[
                x.Year, Minor(x.IncomeMinorUnits), Minor(x.PaidOutflowsMinorUnits), Minor(x.ReservesMinorUnits),
                Minor(x.ObligationsMinorUnits), Minor(x.LoanPaymentsMinorUnits), Minor(x.CollaboratorFundMinorUnits),
                Minor(x.ResultMinorUnits), Minor(x.AccountsReceivableMinorUnits), Minor(x.AccountsPayableMinorUnits),
                Minor(x.PendingReservesMinorUnits), Minor(x.PendingLoansMinorUnits), Minor(x.SurplusMinorUnits),
                Minor(x.DeficitMinorUnits), Minor(x.AvailableBalanceMinorUnits),
                Minor(x.ProjectedNextYearBalanceMinorUnits), currency, x.ClosedUtc.ToLocalTime(), "Confirmado"]),
            moneyColumns: Enumerable.Range(2, 15).ToArray());
        AddTable(workbook, "Saldos arrastrados", ["Año origen", "Año destino", "Cuentas por cobrar", "Cuentas por pagar", "Reservas pendientes", "Préstamos pendientes", "Superávit", "Déficit", "Moneda", "Estado"],
            data.AnnualCarryovers.OrderBy(x => x.SourceYear).Select(x => (object?[])[
                x.SourceYear, x.TargetYear, Minor(x.AccountsReceivableMinorUnits), Minor(x.AccountsPayableMinorUnits),
                Minor(x.PendingReservesMinorUnits), Minor(x.PendingLoansMinorUnits), Minor(x.SurplusMinorUnits),
                Minor(x.DeficitMinorUnits), currency, "Arrastrado al nuevo año"]), moneyColumns: [3, 4, 5, 6, 7, 8]);

        AddTable(workbook, "Flujo de caja", ["Fecha", "Origen", "Concepto", "Entrada o salida", "Moneda"],
            BuildCashMovements(data).OrderBy(x => x.Date).Select(x => (object?[])
            [Date(x.Date), SafeText(x.Category), SafeText(x.Concept), Minor(x.SignedMinorUnits), currency]), moneyColumns: [4]);

        AddTable(workbook, "Movimientos generales", ["Fecha", "Fecha y hora exacta", "Módulo", "Acción", "Resumen", "Descripción", "Importe", "Moneda", "Estado"],
            data.ActivityRecords.OrderBy(x => x.OccurredUtc).Select(x => (object?[])
            [Date(x.ActivityDate), x.OccurredUtc.ToLocalTime(), SafeText(x.Module), SafeText(x.Action), SafeText(x.Summary), SafeText(x.Description ?? ""), ActivityAmount(data, x), currency,
                x.Action.Contains("Eliminación", StringComparison.OrdinalIgnoreCase) ? "Eliminado lógicamente" : "Registrado"]), moneyColumns: [7]);

        AddTable(workbook, "Historial eliminado", ["Tipo de registro", "Descripción", "Fecha de eliminación", "Estado"],
            snapshot.Deleted.OrderBy(x => x.DeletedUtc).Select(x => (object?[])[x.Type, SafeText(x.Description), x.DeletedUtc?.ToLocalTime(), "Eliminado lógicamente"]));

        AddTable(workbook, "Borradores sin finalizar", ["Módulo", "Formulario", "Clasificación", "Contenido técnico del borrador", "Última modificación"],
            snapshot.Drafts.Select(x => (object?[])[SafeText(x.Module), SafeText(x.FormType), x.Classification, SafeText(x.Payload), x.UpdatedUtc.ToLocalTime()]));

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
        object?[][] rows =
        [
            ["Personas con uso del local", data.LocalUsePeople.Count, "registros"],
            ["Deuda total por uso del local", localDebt, currency],
            ["Productos activos", data.Products.Count, "registros"],
            ["Valor estimado del inventario actual", inventory, currency],
            ["Obligaciones pendientes", data.Obligations.Sum(o => Math.Max(0, o.ExpectedAmount.ToDecimal() - data.ObligationPayments.Where(p => p.ObligationId == o.Id).Sum(p => p.Amount.ToDecimal()))), currency],
            ["Borradores sin finalizar", snapshot.Drafts.Count, "no registrados"],
            ["Registros eliminados lógicamente", snapshot.Deleted.Count, "historial"],
        ];
        WriteTable(sheet, 7, ["Indicador", "Valor", "Unidad o moneda"], rows);
        foreach (int row in new[] { 9, 11, 12 }) sheet.Cell(row, 2).Style.NumberFormat.Format = MoneyFormat;
        sheet.SheetView.FreezeRows(1);
        FinalizeSheet(sheet);
    }

    private static void AddMonthlySummaries(XLWorkbook workbook, AdministrationData data, GeneralSettings settings, string currency, DateOnly? from, DateOnly? to)
    {
        if (!from.HasValue || !to.HasValue)
        {
            AddTable(workbook, "Resúmenes mensuales", ["Mes", "Ingresos cobrados", "Cuentas por cobrar", "Egresos pagados", "Cuentas por pagar", "Reservas nuevas", "Reservas arrastradas", "Ajustes de reservas", "Préstamos pagados", "Financiación recibida", "Resultado distribuible", "Punto de equilibrio", "Faltante", "Fondo colaboradores", "Retenido por el local", "Moneda", "Origen"], []);
            return;
        }

        var months = MonthsBetween(from.Value, to.Value).Select(month =>
        {
            MonthlyClose? close = data.MonthlyCloses.Where(x => x.Month == month && x.IsConfirmed).OrderByDescending(x => x.ClosedUtc).FirstOrDefault();
            FinancialMonthSnapshot result = close?.ToFinancialSnapshot() ?? FinancialMonthCalculator.Calculate(data, settings.CollaboratorProfit, month);
            return (object?[])[Date(month.FirstDay), Minor(result.CollectedOperatingIncomeMinorUnits), Minor(result.AccountsReceivableMinorUnits), Minor(result.PaidOutflowsMinorUnits), Minor(result.AccountsPayableMinorUnits), Minor(result.NewReservesMinorUnits), Minor(result.CarriedReservesMinorUnits), Minor(result.ReserveAdjustmentsMinorUnits), Minor(result.LoanPaymentsMinorUnits), Minor(result.FinancingReceivedMinorUnits), Minor(result.DistributableResultMinorUnits), Minor(result.BreakEvenMinorUnits), Minor(result.ShortfallMinorUnits), Minor(result.CollaboratorFundMinorUnits), Minor(result.RetainedLocalMinorUnits), currency, close is not null ? "Snapshot de cierre confirmado" : "Proyección actual"];
        });
        AddTable(workbook, "Resúmenes mensuales", ["Mes", "Ingresos cobrados", "Cuentas por cobrar", "Egresos pagados", "Cuentas por pagar", "Reservas nuevas", "Reservas arrastradas", "Ajustes de reservas", "Préstamos pagados", "Financiación recibida", "Resultado distribuible", "Punto de equilibrio", "Faltante", "Fondo colaboradores", "Retenido por el local", "Moneda", "Origen"], months, moneyColumns: Enumerable.Range(2, 14).ToArray());
    }

    private static void AddAnnualBalances(XLWorkbook workbook, AdministrationData data, GeneralSettings settings, string currency, DateOnly? from, DateOnly? to)
    {
        IEnumerable<int> years = from.HasValue && to.HasValue ? Enumerable.Range(from.Value.Year, to.Value.Year - from.Value.Year + 1) : [];
        var rows = years.Select(year =>
        {
            AnnualAdministrationReport report = AdministrationReports.Annual(data, settings.CollaboratorProfit, year);
            MonthlyExpenseBreakdown e = report.Expenses;
            MonthlyClose[] closes = data.MonthlyCloses.Where(x => x.IsConfirmed && x.Month.Year == year)
                .GroupBy(x => x.Month.Month)
                .Select(group => group.OrderByDescending(x => x.ClosedUtc).First())
                .ToArray();
            long income = closes.Sum(x => x.IncomeMinorUnits);
            long paidOutflows = closes.Sum(x => x.PaidOutflowsMinorUnits);
            long reserves = closes.Sum(x => x.NewReservesMinorUnits);
            long payables = closes.Sum(x => x.AccountsPayableMinorUnits);
            long loans = closes.Sum(x => x.LoanPaymentsMinorUnits);
            long fund = closes.Sum(x => x.FundMinorUnits);
            long retained = closes.Sum(x => x.RetainedResultMinorUnits);
            long missing = closes.Sum(x => x.ShortfallMinorUnits);
            return (object?[])[year, Minor(income), Minor(paidOutflows), Minor(reserves), Minor(payables), Minor(loans),
                Minor(fund), Minor(retained), Minor(missing), Minor(e.ServicesMinorUnits), Minor(e.TaxesMinorUnits),
                Minor(e.OtherObligationsMinorUnits), Minor(e.MerchandiseMinorUnits), Minor(e.MandatorySuppliesMinorUnits),
                Minor(e.OptionalSuppliesMinorUnits), Minor(e.MaintenanceMinorUnits), Minor(e.UnexpectedMinorUnits),
                Minor(e.OtherExpensesMinorUnits), Minor(e.HistoricalAdjustmentMinorUnits), currency,
                closes.Sum(x => x.BaseResultMinorUnits) >= 0 ? "Positivo" : "Negativo"];
        });
        AddTable(workbook, "Balance anual", ["Año", "Ingresos cobrados", "Egresos pagados", "Reservas", "Cuentas por pagar", "Préstamos", "Fondo de colaboradores", "Resultado retenido", "Faltante", "Servicios", "Impuestos", "Otras obligaciones", "Mercancía", "Insumos obligatorios", "Insumos opcionales", "Mantenimiento", "Imprevistos", "Otros gastos", "Ajuste histórico", "Moneda", "Indicador"], rows, moneyColumns: Enumerable.Range(2, 18).ToArray());
    }

    private static IReadOnlyList<CashMovement> BuildCashMovements(AdministrationData data)
    {
        var result = new List<CashMovement>();
        Guid[] confirmedCloseIds = data.MonthlyCloses.Where(x => x.IsConfirmed).Select(x => x.Id).ToArray();
        Guid[] participantIds = data.MonthlyCloseParticipants.Where(x => confirmedCloseIds.Contains(x.CloseId)).Select(x => x.Id).ToArray();
        result.AddRange(data.LocalUsePayments.Select(x => new CashMovement(x.PaymentDate, "Uso del local", $"Pago de {data.LocalUsePeople.SingleOrDefault(p => p.Id == x.PersonId)?.Name ?? "Persona eliminada"}", x.Amount.MinorUnits)));
        result.AddRange(data.InventoryMovements.Where(x => x.Type == InventoryMovementType.Sale).Select(x => new CashMovement(x.Date, "Ventas", ProductName(data, x.ProductId), x.CashAmount?.MinorUnits ?? 0)));
        result.AddRange(data.InventoryMovements.Where(x => x.Type == InventoryMovementType.Purchase).Select(x => new CashMovement(x.Date, "Compras", ProductName(data, x.ProductId), -(x.CashAmount?.MinorUnits ?? 0))));
        result.AddRange(data.FinancialEntries.Select(x => new CashMovement(x.Date, SpanishText.For(x.Type), x.Concept, x.Type == FinancialEntryType.OtherIncome ? x.Amount.MinorUnits : -x.Amount.MinorUnits)));
        result.AddRange(data.ObligationPayments.Select(x => new CashMovement(x.Date, "Obligaciones", ObligationName(data, x.ObligationId), -x.Amount.MinorUnits)));
        result.AddRange(data.MaintenanceRecords.Where(x => x.CompletedDate.HasValue && x.ActualCost.HasValue).Select(x => new CashMovement(x.CompletedDate!.Value, "Mantenimiento", x.Asset, -x.ActualCost!.Value.MinorUnits)));
        result.AddRange(data.DistributionPayments.Where(x => participantIds.Contains(x.ParticipantId)).Select(x => new CashMovement(x.Date, "Pagos a colaboradores", "Distribución pagada", -x.Amount.MinorUnits)));
        result.AddRange(data.Loans.Select(x => new CashMovement(x.StartDate, "Financiación / préstamos", x.Name, x.InitialBalance.MinorUnits)));
        result.AddRange(data.LoanPayments.Select(x => new CashMovement(x.Date, "Financiación / préstamos", data.Loans.SingleOrDefault(loan => loan.Id == x.LoanId)?.Name ?? "Préstamo eliminado", -x.Amount.MinorUnits)));
        result.AddRange(data.CollaboratorContributions.Select(x => new CashMovement(x.Date, "Financiación / aportes", CollaboratorName(data, new Dictionary<Guid, string>(), x.CollaboratorId), x.Amount.MinorUnits)));
        return result;
    }

    private static decimal? ActivityAmount(AdministrationData data, ActivityRecord activity)
    {
        long? minorUnits = data.LocalUsePayments.SingleOrDefault(x => x.Id == activity.EntityId)?.Amount.MinorUnits
            ?? data.InventoryMovements.SingleOrDefault(x => x.Id == activity.EntityId)?.CashAmount?.MinorUnits
            ?? data.FinancialEntries.SingleOrDefault(x => x.Id == activity.EntityId)?.Amount.MinorUnits
            ?? data.ObligationPayments.SingleOrDefault(x => x.Id == activity.EntityId)?.Amount.MinorUnits
            ?? data.DistributionPayments.SingleOrDefault(x => x.Id == activity.EntityId)?.Amount.MinorUnits
            ?? data.LoanPayments.SingleOrDefault(x => x.Id == activity.EntityId)?.Amount.MinorUnits
            ?? data.FinancialReserves.SingleOrDefault(x => x.Id == activity.EntityId)?.ReservedAmount.MinorUnits
            ?? data.MonthlyCloses.SingleOrDefault(x => x.Id == activity.EntityId)?.FundMinorUnits
            ?? data.AnnualCloses.SingleOrDefault(x => x.Id == activity.EntityId)?.ResultMinorUnits;
        return minorUnits.HasValue ? Minor(minorUnits.Value) : null;
    }

    private static void AddFinancialSheet(XLWorkbook workbook, string name, AdministrationData data, FinancialEntryType type, string currency) =>
        AddTable(workbook, name, ["Fecha", "Concepto", "Categoría", "Valor", "Moneda", "Descripción", "Estado"],
            data.FinancialEntries.Where(x => x.Type == type).OrderBy(x => x.Date).Select(x => (object?[])
            [Date(x.Date), SafeText(x.Concept), x.Category.HasValue ? SpanishText.For(x.Category.Value) : "No aplica", x.Amount.ToDecimal(), currency, SafeText(x.Description ?? ""), "Registrado"]), moneyColumns: [4]);

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
        dates.AddRange(data.InventoryMovements.Select(x => x.Date)); dates.AddRange(data.FinancialEntries.Select(x => x.Date));
        dates.AddRange(data.Obligations.Select(x => x.DueDate)); dates.AddRange(data.ObligationPayments.Select(x => x.Date)); dates.AddRange(data.MaintenanceRecords.Select(x => x.ScheduledDate));
        dates.AddRange(data.MaintenanceRecords.Where(x => x.CompletedDate.HasValue).Select(x => x.CompletedDate!.Value)); dates.AddRange(data.Collaborators.Select(x => x.StartDate));
        dates.AddRange(data.MonthlyCloses.Select(x => x.Month.FirstDay)); dates.AddRange(data.DistributionPayments.Select(x => x.Date));
        dates.AddRange(data.CollaboratorContributions.Select(x => x.Date));
        dates.AddRange(data.CollaboratorContributionEvents.Select(x => x.EffectiveDate));
        dates.AddRange(data.MonthlyPurchaseItems.Select(x => x.Month.FirstDay));
        dates.AddRange(data.Loans.Select(x => x.StartDate)); dates.AddRange(data.Loans.Select(x => x.NextDueDate));
        dates.AddRange(data.LoanInstallments.Select(x => x.DueDate));
        dates.AddRange(data.LoanPayments.Select(x => x.Date)); dates.AddRange(data.FinancialReserves.Select(x => x.DueDate));
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
    private static string PersonName(AdministrationData data, IReadOnlyDictionary<Guid, string> deleted, Guid id) =>
        data.LocalUsePeople.SingleOrDefault(x => x.Id == id)?.Name
        ?? (deleted.TryGetValue(id, out string? name) ? name : "Persona eliminada");
    private static string ProductName(AdministrationData data, Guid id) => data.Products.SingleOrDefault(x => x.Id == id)?.Name ?? "Producto eliminado";
    private static string ObligationName(AdministrationData data, Guid id) => data.Obligations.SingleOrDefault(x => x.Id == id)?.Name ?? "Obligación eliminada";
    private static string CollaboratorName(AdministrationData data, IReadOnlyDictionary<Guid, string> deleted, Guid id) =>
        data.Collaborators.SingleOrDefault(x => x.Id == id)?.Name
        ?? (deleted.TryGetValue(id, out string? name) ? name : "Colaborador eliminado");
    private static string MovementName(InventoryMovementType type) => type switch { InventoryMovementType.InitialStock => "Existencia inicial", InventoryMovementType.Purchase => "Compra", InventoryMovementType.Sale => "Venta", InventoryMovementType.InternalConsumption => "Consumo interno", InventoryMovementType.PhysicalCountAdjustment => "Ajuste por conteo físico", _ => type.ToString() };

    private static string CommitmentSourceName(FinancialCommitmentSource source) => source switch { FinancialCommitmentSource.Obligation => "Obligación", FinancialCommitmentSource.Maintenance => "Mantenimiento", FinancialCommitmentSource.MonthlyPurchase => "Lista mensual de compra", FinancialCommitmentSource.LoanInstallment => "Cuota de préstamo", _ => "Compromiso anterior" };

    private static string MaintenanceFrequencyName(MaintenanceFrequency frequency) => frequency switch { MaintenanceFrequency.Weekly => "Semanal", MaintenanceFrequency.Biweekly => "Quincenal", MaintenanceFrequency.Monthly => "Mensual", MaintenanceFrequency.EveryTwoMonths => "Cada 2 meses", MaintenanceFrequency.EveryThreeMonths => "Cada 3 meses", MaintenanceFrequency.EverySixMonths => "Cada 6 meses", MaintenanceFrequency.Yearly => "Anual", MaintenanceFrequency.Custom => "Personalizada", _ => "Una vez" };

    private static string MaintenanceUnitName(MaintenanceIntervalUnit? unit) => unit switch { MaintenanceIntervalUnit.Days => "Días", MaintenanceIntervalUnit.Weeks => "Semanas", MaintenanceIntervalUnit.Months => "Meses", MaintenanceIntervalUnit.Years => "Años", _ => string.Empty };

    private sealed record ExportSnapshot(
        GeneralSettings Settings,
        AdministrationData Active,
        IReadOnlyList<DeletedRecord> Deleted,
        IReadOnlyList<DraftRecord> Drafts,
        string? NoteContent,
        DateTime? NoteUpdatedUtc,
        IReadOnlyDictionary<Guid, string> DeletedPeople,
        IReadOnlyDictionary<Guid, string> DeletedCollaborators);
    private sealed record DeletedRecord(string Type, string Description, DateTime? DeletedUtc);
    private sealed record DraftRecord(string Module, string FormType, string Classification, string Payload, DateTime UpdatedUtc);
}
