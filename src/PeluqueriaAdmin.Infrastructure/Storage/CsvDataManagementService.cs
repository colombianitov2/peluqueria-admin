using System.Globalization;
using System.Text;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.DataManagement;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Infrastructure.Storage;

public sealed class CsvDataManagementService(
    AdministrationService administrationService,
    ISettingsRepository settingsRepository,
    DatabaseBackupService backupService,
    ApplicationPaths paths,
    TimeProvider timeProvider) : IDataManagementService
{
    public string BackupsDirectory => paths.BackupsDirectory;

    public string ExportsDirectory => paths.ExportsDirectory;

    public Task<string> CreateManualBackupAsync(CancellationToken cancellationToken = default) =>
        backupService.CreateManualAsync(cancellationToken);

    public Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default) =>
        backupService.RestoreAsync(backupFilePath, cancellationToken);

    public async Task<IReadOnlyList<string>> ExportAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        AdministrationData data = await administrationService.GenerateScheduledRecordsAsync(
            new DateOnly(today.Year, 12, 31), cancellationToken);
        string stamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var files = new List<string>();

        files.Add(await WriteAsync(
            $"resumen-mensual-{stamp}.csv",
            BuildMonthlySummaries(data, settings, today.Year),
            cancellationToken));
        files.Add(await WriteAsync(
            $"balance-anual-{stamp}.csv",
            BuildAnnualBalance(data, settings, today.Year),
            cancellationToken));
        files.Add(await WriteAsync(
            $"flujo-caja-{stamp}.csv",
            BuildCashFlow(data),
            cancellationToken));
        files.Add(await WriteAsync(
            $"inventario-actual-{stamp}.csv",
            BuildInventory(data),
            cancellationToken));
        files.Add(await WriteAsync(
            $"deudas-uso-local-{stamp}.csv",
            BuildDebts(data, settings.CurrencyCode.Value),
            cancellationToken));
        files.Add(await WriteAsync(
            $"aportes-colaboradores-{stamp}.csv",
            BuildContributions(data, settings.CurrencyCode.Value),
            cancellationToken));
        return files;
    }

    private string BuildMonthlySummaries(
        AdministrationData data,
        GeneralSettings settings,
        int year)
    {
        var csv = new CsvBuilder("Mes", "Moneda", "Ingresos", "Meta", "Faltante", "Resultado base", "Fondo colaboradores", "Resultado retenido");
        foreach (int monthNumber in Enumerable.Range(1, 12))
        {
            var month = new YearMonth(year, monthNumber);
            MonthlySummaryResult result = AdministrationReports.MonthlySummary(
                data, settings.OptionalSuppliesMonthlyBudget, settings.CollaboratorProfit, month);
            csv.Add(
                month.ToString(),
                settings.CurrencyCode.Value,
                Decimal(result.IncomeMinorUnits),
                Decimal(result.GoalMinorUnits),
                Decimal(result.MissingMinorUnits),
                Decimal(result.BaseResultMinorUnits),
                Decimal(result.CollaboratorFundMinorUnits),
                Decimal(result.RetainedResultMinorUnits));
        }

        return csv.ToString();
    }

    private string BuildAnnualBalance(AdministrationData data, GeneralSettings settings, int year)
    {
        AnnualAdministrationReport report = AdministrationReports.Annual(
            data, settings.OptionalSuppliesMonthlyBudget, settings.CollaboratorProfit, year);
        AnnualBalanceResult result = report.Balance;
        MonthlyExpenseBreakdown expenses = report.Expenses;
        var csv = new CsvBuilder(
            "Año", "Moneda", "Ingresos", "Gastos/meta", "Servicios", "Impuestos",
            "Otras obligaciones", "Mercancía para venta", "Insumos obligatorios",
            "Insumos opcionales", "Mantenimiento", "Imprevistos", "Otros gastos",
            "Planes de reposición", "Ajuste histórico", "Distribuciones pagadas",
            "Resultado retenido", "Pendientes", "Faltante", "Indicador");
        csv.Add(
            year.ToString(CultureInfo.InvariantCulture),
            settings.CurrencyCode.Value,
            Decimal(result.IncomeMinorUnits),
            Decimal(result.ExpenseMinorUnits),
            Decimal(expenses.ServicesMinorUnits),
            Decimal(expenses.TaxesMinorUnits),
            Decimal(expenses.OtherObligationsMinorUnits),
            Decimal(expenses.MerchandiseMinorUnits),
            Decimal(expenses.MandatorySuppliesMinorUnits),
            Decimal(expenses.OptionalSuppliesMinorUnits),
            Decimal(expenses.MaintenanceMinorUnits),
            Decimal(expenses.UnexpectedMinorUnits),
            Decimal(expenses.OtherExpensesMinorUnits),
            Decimal(expenses.PendingPlansMinorUnits),
            Decimal(expenses.HistoricalAdjustmentMinorUnits),
            Decimal(result.DistributionMinorUnits),
            Decimal(result.RetainedMinorUnits),
            Decimal(result.PendingMinorUnits),
            Decimal(result.MissingMinorUnits),
            report.Indicator);
        return csv.ToString();
    }

    private static string BuildCashFlow(AdministrationData data)
    {
        var rows = new List<CashMovement>();
        Guid[] confirmedCloseIds = data.MonthlyCloses.Where(item => item.IsConfirmed).Select(item => item.Id).ToArray();
        Guid[] validParticipantIds = data.MonthlyCloseParticipants
            .Where(item => confirmedCloseIds.Contains(item.CloseId))
            .Select(item => item.Id)
            .ToArray();
        rows.AddRange(data.LocalUsePayments.Select(item =>
            new CashMovement(item.PaymentDate, "Uso del local", "Pago recibido", item.Amount.MinorUnits)));
        rows.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale).Select(item =>
            new CashMovement(item.Date, "Ventas", "Venta", item.CashAmount?.MinorUnits ?? 0)));
        rows.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Purchase).Select(item =>
            new CashMovement(item.Date, "Compras", "Compra de inventario", -(item.CashAmount?.MinorUnits ?? 0))));
        rows.AddRange(data.FinancialEntries.Select(item => new CashMovement(
            item.Date,
            SpanishText.For(item.Type),
            item.Concept,
            item.Type == FinancialEntryType.OtherIncome ? item.Amount.MinorUnits : -item.Amount.MinorUnits)));
        rows.AddRange(data.ObligationPayments.Select(item =>
            new CashMovement(item.Date, "Obligaciones", "Pago", -item.Amount.MinorUnits)));
        rows.AddRange(data.MaintenanceRecords
            .Where(item => item.CompletedDate.HasValue && item.ActualCost.HasValue)
            .Select(item => new CashMovement(
                item.CompletedDate!.Value, "Mantenimiento", item.Asset, -item.ActualCost!.Value.MinorUnits)));
        rows.AddRange(data.DistributionPayments.Where(item => validParticipantIds.Contains(item.ParticipantId)).Select(item =>
            new CashMovement(item.Date, "Nómina de colaboradores", "Distribución", -item.Amount.MinorUnits)));

        var csv = new CsvBuilder("Fecha", "Categoría", "Concepto", "Entrada", "Salida");
        foreach (CashMovement row in rows.OrderBy(item => item.Date))
        {
            csv.Add(
                row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.Category,
                row.Concept,
                row.SignedMinorUnits >= 0 ? Decimal(row.SignedMinorUnits) : "0.00",
                row.SignedMinorUnits < 0 ? Decimal(-row.SignedMinorUnits) : "0.00");
        }

        return csv.ToString();
    }

    private static string BuildInventory(AdministrationData data)
    {
        var csv = new CsvBuilder("Producto", "Categoría", "Existencia", "Costo unitario promedio");
        foreach (Product product in data.Products)
        {
            InventoryMovement[] movements = data.InventoryMovements
                .Where(item => item.ProductId == product.Id)
                .ToArray();
            csv.Add(
                product.Name,
                SpanishText.For(product.Category),
                InventoryCalculator.CurrentQuantity(movements).ToString("0.###", CultureInfo.InvariantCulture),
                InventoryCalculator.AverageUnitCost(movements).ToDecimal().ToString("0.00", CultureInfo.InvariantCulture));
        }

        return csv.ToString();
    }

    private static string BuildContributions(AdministrationData data, string currency)
    {
        var csv = new CsvBuilder("Fecha", "Colaborador", "Moneda", "Valor", "Descripción", "Clasificación");
        foreach (var contribution in data.CollaboratorContributions.OrderBy(item => item.Date).ThenBy(item => item.CreatedUtc))
        {
            string collaborator = data.Collaborators
                .SingleOrDefault(item => item.Id == contribution.CollaboratorId)?.Name ?? "Colaborador no disponible";
            csv.Add(
                contribution.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                collaborator,
                currency,
                Decimal(contribution.Amount.MinorUnits),
                contribution.Description ?? string.Empty,
                "Capital / inversión no operativa");
        }

        return csv.ToString();
    }

    private static string BuildDebts(AdministrationData data, string currency)
    {
        var csv = new CsvBuilder("Persona", "Moneda", "Cuotas generadas", "Pagos", "Deuda");
        foreach (LocalUsePerson person in data.LocalUsePeople)
        {
            WeeklyCharge[] charges = data.WeeklyCharges.Where(item => item.PersonId == person.Id).ToArray();
            LocalUsePayment[] payments = data.LocalUsePayments.Where(item => item.PersonId == person.Id).ToArray();
            long charged = charges.Sum(item => item.Amount.MinorUnits);
            long paid = payments.Sum(item => item.Amount.MinorUnits);
            csv.Add(person.Name, currency, Decimal(charged), Decimal(paid), Decimal(charged - paid));
        }

        return csv.ToString();
    }

    private async Task<string> WriteAsync(
        string fileName,
        string content,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(paths.ExportsDirectory, fileName);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken);
        return path;
    }

    private static string Decimal(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private sealed class CsvBuilder(params string[] headers)
    {
        private readonly StringBuilder builder = new StringBuilder()
            .AppendLine(string.Join(',', headers.Select(Escape)));

        public void Add(params string[] values) =>
            builder.AppendLine(string.Join(',', values.Select(Escape)));

        public override string ToString() => builder.ToString();

        private static string Escape(string value) =>
            $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
