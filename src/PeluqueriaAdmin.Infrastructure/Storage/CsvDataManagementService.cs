using System.Globalization;
using System.Text;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.DataManagement;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Infrastructure.Storage;

public sealed class CsvDataManagementService(
    IAdministrationRepository administrationRepository,
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
        AdministrationData data = await administrationRepository.LoadAsync(cancellationToken);
        GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
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
            MonthlySummaryResult result = MonthlySummaryCalculator.Calculate(
                BuildMonthlyInput(data, settings, month),
                settings.CollaboratorProfit);
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
        MonthlySummaryResult[] months = Enumerable.Range(1, 12)
            .Select(month => MonthlySummaryCalculator.Calculate(
                BuildMonthlyInput(data, settings, new YearMonth(year, month)),
                settings.CollaboratorProfit))
            .ToArray();
        long paidDistributions = data.DistributionPayments
            .Where(item => item.Date.Year == year)
            .Sum(item => item.Amount.MinorUnits);
        long pending = data.Obligations.Where(item => item.DueDate.Year == year).Sum(item => Math.Max(
            0,
            item.ExpectedAmount.MinorUnits - data.ObligationPayments
                .Where(payment => payment.ObligationId == item.Id)
                .Sum(payment => payment.Amount.MinorUnits)));
        AnnualBalanceResult result = AnnualBalanceCalculator.Calculate(months, paidDistributions, pending);
        var csv = new CsvBuilder("Año", "Moneda", "Ingresos", "Gastos/meta", "Distribuciones pagadas", "Resultado retenido", "Pendientes", "Faltante");
        csv.Add(
            year.ToString(CultureInfo.InvariantCulture),
            settings.CurrencyCode.Value,
            Decimal(result.IncomeMinorUnits),
            Decimal(result.ExpenseMinorUnits),
            Decimal(result.DistributionMinorUnits),
            Decimal(result.RetainedMinorUnits),
            Decimal(result.PendingMinorUnits),
            Decimal(result.MissingMinorUnits));
        return csv.ToString();
    }

    private static string BuildCashFlow(AdministrationData data)
    {
        var rows = new List<CashMovement>();
        rows.AddRange(data.LocalUsePayments.Select(item =>
            new CashMovement(item.PaymentDate, "Uso del local", "Pago recibido", item.Amount.MinorUnits)));
        rows.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale).Select(item =>
            new CashMovement(item.Date, "Ventas", "Venta", item.CashAmount?.MinorUnits ?? 0)));
        rows.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Purchase).Select(item =>
            new CashMovement(item.Date, "Compras", "Compra de inventario", -(item.CashAmount?.MinorUnits ?? 0))));
        rows.AddRange(data.FinancialEntries.Select(item => new CashMovement(
            item.Date,
            item.Type.ToString(),
            item.Concept,
            item.Type == FinancialEntryType.OtherIncome ? item.Amount.MinorUnits : -item.Amount.MinorUnits)));
        rows.AddRange(data.ObligationPayments.Select(item =>
            new CashMovement(item.Date, "Obligaciones", "Pago", -item.Amount.MinorUnits)));
        rows.AddRange(data.MaintenanceRecords
            .Where(item => item.CompletedDate.HasValue && item.ActualCost.HasValue)
            .Select(item => new CashMovement(
                item.CompletedDate!.Value, "Mantenimiento", item.Asset, -item.ActualCost!.Value.MinorUnits)));
        rows.AddRange(data.DistributionPayments.Select(item =>
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
        var csv = new CsvBuilder("Producto", "Categoría", "Unidad", "Existencia", "Costo unitario promedio");
        foreach (Product product in data.Products)
        {
            InventoryMovement[] movements = data.InventoryMovements
                .Where(item => item.ProductId == product.Id)
                .ToArray();
            csv.Add(
                product.Name,
                product.Category.ToString(),
                product.UnitOfMeasure,
                InventoryCalculator.CurrentQuantity(movements).ToString("0.###", CultureInfo.InvariantCulture),
                InventoryCalculator.AverageUnitCost(movements).ToDecimal().ToString("0.00", CultureInfo.InvariantCulture));
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

    private static MonthlySummaryInput BuildMonthlyInput(
        AdministrationData data,
        GeneralSettings settings,
        YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        InventoryMovement[] purchases = data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Purchase && InMonth(item.Date))
            .ToArray();
        long PurchaseFor(ProductCategory category) => purchases
            .Where(item => data.Products.Any(product => product.Id == item.ProductId && product.Category == category))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long mandatory = data.FinancialEntries
            .Where(item => item.Type == FinancialEntryType.Expense
                && item.Category != ExpenseCategory.OptionalSupply
                && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits)
            + PurchaseFor(ProductCategory.MandatorySupply)
            + PurchaseFor(ProductCategory.DurableEquipment);
        long optional = data.FinancialEntries
            .Where(item => item.Type == FinancialEntryType.Expense
                && item.Category == ExpenseCategory.OptionalSupply
                && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits)
            + PurchaseFor(ProductCategory.OptionalCustomerSupply);
        long plans = data.RestockPlans.Where(item => item.Month == month).Sum(plan =>
        {
            InventoryMovement[] movements = data.InventoryMovements
                .Where(item => item.ProductId == plan.ProductId && item.Date <= month.LastDay)
                .ToArray();
            decimal quantity = plan.SuggestedPurchase(InventoryCalculator.CurrentQuantity(movements));
            return checked((long)decimal.Round(
                InventoryCalculator.AverageUnitCost(movements).MinorUnits * quantity,
                0,
                MidpointRounding.AwayFromZero));
        });

        return new MonthlySummaryInput(
            data.LocalUsePayments.Where(item => InMonth(item.PaymentDate)).Sum(item => item.Amount.MinorUnits),
            data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
                .Sum(item => item.CashAmount?.MinorUnits ?? 0),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.Obligations.Where(item => InMonth(item.DueDate))
                .Sum(item => item.GoalAmount(data.ObligationPayments).MinorUnits),
            PurchaseFor(ProductCategory.ProductForSale),
            mandatory,
            optional,
            settings.OptionalSuppliesMonthlyBudget.MinorUnits,
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.UnexpectedExpense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.MaintenanceRecords.Sum(item => item.GoalAmountFor(month).MinorUnits),
            plans);
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
