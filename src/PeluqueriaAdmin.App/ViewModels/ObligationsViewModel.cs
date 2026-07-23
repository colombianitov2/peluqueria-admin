using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class ObligationsViewModel(AdministrationService service, TimeProvider timeProvider) : ObservableObject
{
    public ObservableCollection<string> TypeOptions { get; } = ["Servicio", "Impuesto", "Otra obligación"];
    public ObservableCollection<string> RecurrenceOptions { get; } = ["Sin recurrencia", "Mensual", "Anual"];
    public ObservableCollection<ObligationCatalogRow> Obligations { get; } = [];
    public ObservableCollection<ObligationPaymentListRow> Payments { get; } = [];
    public ObservableCollection<ObligationSeriesOption> PaymentOptions { get; } = [];
    public ObservableCollection<LoanRow> Loans { get; } = [];
    public ObservableCollection<LoanInstallmentRow> LoanInstallments { get; } = [];
    public ObservableCollection<LoanPaymentRow> LoanPayments { get; } = [];
    public ObservableCollection<string> LoanCalculationMethodOptions { get; } =
        ["Interés mensual sobre saldo", "Cantidad final acordada"];

    [ObservableProperty] private bool isAddMode = true;
    [ObservableProperty] private bool isLoanMode;
    [ObservableProperty] private string nameText = string.Empty;
    [ObservableProperty] private string selectedType = "Servicio";
    [ObservableProperty] private string selectedRecurrence = "Sin recurrencia";
    [ObservableProperty] private DateTime? initialDueDate = DateTime.Today;
    [ObservableProperty] private string expectedAmountText = string.Empty;
    [ObservableProperty] private string obligationDescription = string.Empty;
    [ObservableProperty] private ObligationCatalogRow? selectedObligation;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool confirmDelete;
    [ObservableProperty] private ObligationSeriesOption? selectedPaymentOption;
    [ObservableProperty] private DateTime? paymentDate = DateTime.Today;
    [ObservableProperty] private string paymentAmountText = string.Empty;
    [ObservableProperty] private string paymentDescription = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;

    [ObservableProperty] private string loanName = string.Empty;
    [ObservableProperty] private string loanInitialBalance = string.Empty;
    [ObservableProperty] private string selectedLoanCalculationMethod = "Interés mensual sobre saldo";
    [ObservableProperty] private string loanMonthlyInterestPercent = string.Empty;
    [ObservableProperty] private string loanAgreedFinalAmount = string.Empty;
    [ObservableProperty] private string loanInstallmentCount = string.Empty;
    [ObservableProperty] private DateTime? loanFirstDueDate = DateTime.Today;
    [ObservableProperty] private string loanDescription = string.Empty;
    [ObservableProperty] private string loanPreview = "Completa los datos para ver el plan de cuotas.";
    [ObservableProperty] private bool showMonthlyInterest = true;
    [ObservableProperty] private bool showAgreedFinalAmount;
    [ObservableProperty] private LoanRow? selectedLoan;
    [ObservableProperty] private DateTime? loanPaymentDate = DateTime.Today;
    [ObservableProperty] private string loanPaymentAmount = string.Empty;
    [ObservableProperty] private string loanPaymentDescription = string.Empty;

    public bool IsPaymentMode => !IsAddMode && !IsLoanMode;

    public async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        DateOnly today = Today();
        AdministrationData data = await service.GenerateScheduledRecordsAsync(today);
        Guid? selectedSeries = SelectedObligation?.SeriesId;
        Obligations.Clear();
        PaymentOptions.Clear();
        foreach (IGrouping<Guid, Obligation> group in data.Obligations.GroupBy(item => item.SeriesId)
            .OrderBy(item => item.Min(value => value.Name)))
        {
            Obligation definition = group.OrderBy(item => item.DueDate).First();
            Obligation? next = group.OrderBy(item => item.DueDate).FirstOrDefault(item => !IsPaid(item, data));
            var row = new ObligationCatalogRow(
                group.Key,
                definition,
                definition.Name,
                TypeName(definition.Type),
                RecurrenceName(definition.Recurrence),
                next?.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "Sin vencimientos pendientes",
                $"{ApplicationCurrency.Code} {definition.ExpectedAmount.ToDecimal():N2}",
                definition.Description ?? string.Empty);
            Obligations.Add(row);
            PaymentOptions.Add(new ObligationSeriesOption(group.Key, definition.Name));
        }

        Payments.Clear();
        foreach (ObligationPayment payment in data.ObligationPayments.OrderByDescending(item => item.Date).ThenByDescending(item => item.CreatedUtc))
        {
            Obligation? occurrence = data.Obligations.SingleOrDefault(item => item.Id == payment.ObligationId);
            Payments.Add(new ObligationPaymentListRow(
                payment.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                occurrence?.Name ?? "Obligación eliminada",
                occurrence?.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                $"{ApplicationCurrency.Code} {payment.Amount.ToDecimal():N2}",
                payment.Description ?? string.Empty));
        }
        SelectedObligation = selectedSeries.HasValue ? Obligations.SingleOrDefault(item => item.SeriesId == selectedSeries) : null;
        Loans.Clear();
        foreach (Loan loan in data.Loans.OrderBy(item => item.NextDueDate))
            Loans.Add(new LoanRow(loan, loan.Name, $"{ApplicationCurrency.Code} {loan.InitialBalance.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.PendingBalance.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.UsualInstallment.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.ExpectedTotal.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.TotalInterest.ToDecimal():N2}",
                LoanMethodName(loan.CalculationMethod),
                loan.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                loan.NextDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                loan.IsPaid ? "Pagado" : "Pendiente", loan.Description ?? string.Empty));
        LoanInstallments.Clear();
        foreach (LoanInstallment installment in data.LoanInstallments.OrderBy(item => item.DueDate).ThenBy(item => item.Number))
        {
            Loan? loan = data.Loans.SingleOrDefault(item => item.Id == installment.LoanId);
            bool paid = data.LoanPayments.Any(item => item.InstallmentId == installment.Id);
            LoanInstallments.Add(new LoanInstallmentRow(
                installment,
                loan?.Name ?? "Préstamo eliminado",
                installment.Number,
                installment.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                $"{ApplicationCurrency.Code} {installment.Amount.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {installment.Principal.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {installment.Interest.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {installment.PrincipalBalanceAfter.ToDecimal():N2}",
                paid ? "Pagada" : installment.DueDate < today ? "Vencida" : "Pendiente",
                installment.Description ?? string.Empty));
        }
        LoanPayments.Clear();
        foreach (LoanPayment payment in data.LoanPayments.OrderByDescending(item => item.Date))
            LoanPayments.Add(new LoanPaymentRow(payment.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                data.Loans.SingleOrDefault(item => item.Id == payment.LoanId)?.Name ?? "Préstamo eliminado",
                $"{ApplicationCurrency.Code} {payment.Amount.ToDecimal():N2}", payment.Description ?? string.Empty));
    }

    [RelayCommand]
    private void ShowAddMode() { IsLoanMode = false; IsAddMode = true; }

    [RelayCommand]
    private void ShowPaymentMode() { IsLoanMode = false; IsAddMode = false; }

    [RelayCommand]
    private void ShowLoanMode() { IsAddMode = false; IsLoanMode = true; OnPropertyChanged(nameof(IsPaymentMode)); }

    [RelayCommand]
    private async Task AddLoanAsync()
    {
        try
        {
            int count = int.TryParse(LoanInstallmentCount, out int parsed) && parsed > 0
                ? parsed
                : throw new ArgumentException("La cantidad de cuotas debe ser un entero positivo.");
            Money principal = ParseMoney(LoanInitialBalance);
            DateOnly firstDueDate = RequiredDate(LoanFirstDueDate, "fecha de la primera cuota");
            DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
            LoanPlan plan = SelectedLoanCalculationMethod == "Cantidad final acordada"
                ? LoanCalculator.AgreedFinalAmount(
                    LoanName, principal, ParseMoney(LoanAgreedFinalAmount), count,
                    firstDueDate, utcNow, LoanDescription)
                : LoanCalculator.MonthlyBalanceInterest(
                    LoanName, principal, ParseNonNegativeDecimal(LoanMonthlyInterestPercent, "interés mensual"),
                    count, firstDueDate, utcNow, LoanDescription);
            await service.AddLoanAsync(plan);
            ClearLoanForm();
            StatusMessage = "El préstamo y su calendario de cuotas quedaron registrados; la financiación no incrementa la ganancia.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand]
    private async Task RegisterLoanPaymentAsync()
    {
        try
        {
            if (SelectedLoan is null) throw new InvalidOperationException("Selecciona el préstamo pagado.");
            await service.RegisterLoanPaymentAsync(SelectedLoan.Loan.Id, RequiredDate(LoanPaymentDate, "fecha de pago"),
                ParseMoney(LoanPaymentAmount), LoanPaymentDescription);
            LoanPaymentAmount = LoanPaymentDescription = string.Empty;
            StatusMessage = "La cuota exacta se registró y redujo el saldo pendiente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand]
    private async Task AddObligationAsync()
    {
        try
        {
            Obligation obligation = Obligation.Create(
                NameText, ParseType(SelectedType), RequiredDate(InitialDueDate, "fecha de vencimiento inicial"),
                ParseMoney(ExpectedAmountText), ParseRecurrence(SelectedRecurrence),
                timeProvider.GetUtcNow().UtcDateTime, ObligationDescription);
            await service.AddObligationAsync(obligation, Today());
            ResetDefinitionForm();
            StatusMessage = "La obligación se agregó correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand]
    private void EditSelectedObligation()
    {
        if (SelectedObligation is null) return;
        IsAddMode = true;
        IsEditing = true;
        NameText = SelectedObligation.Definition.Name;
        SelectedType = TypeName(SelectedObligation.Definition.Type);
        SelectedRecurrence = RecurrenceName(SelectedObligation.Definition.Recurrence);
        InitialDueDate = SelectedObligation.Definition.DueDate.ToDateTime(TimeOnly.MinValue);
        ExpectedAmountText = SelectedObligation.Definition.ExpectedAmount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        ObligationDescription = SelectedObligation.Definition.Description ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveObligationEditAsync()
    {
        if (!IsEditing || SelectedObligation is null) return;
        try
        {
            await service.UpdateObligationDefinitionAsync(
                SelectedObligation.SeriesId, NameText, ParseType(SelectedType),
                RequiredDate(InitialDueDate, "fecha de vencimiento inicial"), ParseMoney(ExpectedAmountText),
                ParseRecurrence(SelectedRecurrence), ObligationDescription);
            ResetDefinitionForm();
            StatusMessage = "La obligación se actualizó correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedObligationAsync()
    {
        if (SelectedObligation is null || !ConfirmDelete)
        {
            StatusMessage = "Selecciona una obligación y marca la confirmación para eliminarla.";
            IsError = true;
            return;
        }
        try
        {
            await service.DeleteObligationSeriesAsync(SelectedObligation.SeriesId);
            ConfirmDelete = false;
            ResetDefinitionForm();
            StatusMessage = "La obligación se eliminó lógicamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand]
    private async Task RegisterPaymentAsync()
    {
        if (SelectedPaymentOption is null)
        {
            StatusMessage = "Selecciona la obligación pagada.";
            IsError = true;
            return;
        }
        try
        {
            await service.RegisterObligationPaymentAsync(
                SelectedPaymentOption.SeriesId,
                RequiredDate(PaymentDate, "fecha de pago"),
                ParseMoney(PaymentAmountText),
                PaymentDescription);
            PaymentDate = timeProvider.GetLocalNow().DateTime.Date;
            PaymentAmountText = string.Empty;
            PaymentDescription = string.Empty;
            StatusMessage = "El pago se registró y la ocurrencia quedó pagada.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    public Task FlushPendingAsync() => Task.CompletedTask;

    partial void OnIsAddModeChanged(bool value) => OnPropertyChanged(nameof(IsPaymentMode));
    partial void OnIsLoanModeChanged(bool value) => OnPropertyChanged(nameof(IsPaymentMode));
    partial void OnSelectedLoanCalculationMethodChanged(string value)
    {
        ShowMonthlyInterest = value == "Interés mensual sobre saldo";
        ShowAgreedFinalAmount = !ShowMonthlyInterest;
        UpdateLoanPreview();
    }
    partial void OnLoanInitialBalanceChanged(string value) => UpdateLoanPreview();
    partial void OnLoanMonthlyInterestPercentChanged(string value) => UpdateLoanPreview();
    partial void OnLoanAgreedFinalAmountChanged(string value) => UpdateLoanPreview();
    partial void OnLoanInstallmentCountChanged(string value) => UpdateLoanPreview();
    partial void OnLoanFirstDueDateChanged(DateTime? value) => UpdateLoanPreview();
    partial void OnSelectedLoanChanged(LoanRow? value)
    {
        if (value is null) return;
        _ = PrefillNextLoanInstallmentAsync(value.Loan.Id);
    }

    private async Task PrefillNextLoanInstallmentAsync(Guid loanId)
    {
        AdministrationData data = await service.LoadAsync();
        LoanInstallment? installment = data.LoanInstallments
            .Where(item => item.LoanId == loanId
                && data.LoanPayments.All(payment => payment.InstallmentId != item.Id))
            .OrderBy(item => item.Number)
            .FirstOrDefault();
        if (installment is null) return;
        LoanPaymentAmount = installment.Amount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        LoanPaymentDate = installment.DueDate.ToDateTime(TimeOnly.MinValue);
    }

    private void UpdateLoanPreview()
    {
        try
        {
            if (!int.TryParse(LoanInstallmentCount, out int count) || count <= 0
                || !LoanFirstDueDate.HasValue || string.IsNullOrWhiteSpace(LoanInitialBalance))
            {
                LoanPreview = "Completa principal, cantidad de cuotas y primera fecha de pago.";
                return;
            }
            LoanPlan plan = SelectedLoanCalculationMethod == "Cantidad final acordada"
                ? LoanCalculator.AgreedFinalAmount(
                    string.IsNullOrWhiteSpace(LoanName) ? "Vista previa" : LoanName,
                    ParseMoney(LoanInitialBalance), ParseMoney(LoanAgreedFinalAmount), count,
                    DateOnly.FromDateTime(LoanFirstDueDate.Value), timeProvider.GetUtcNow().UtcDateTime)
                : LoanCalculator.MonthlyBalanceInterest(
                    string.IsNullOrWhiteSpace(LoanName) ? "Vista previa" : LoanName,
                    ParseMoney(LoanInitialBalance), ParseNonNegativeDecimal(LoanMonthlyInterestPercent, "interés mensual"),
                    count, DateOnly.FromDateTime(LoanFirstDueDate.Value), timeProvider.GetUtcNow().UtcDateTime);
            LoanPreview =
                $"Cuota inicial: {ApplicationCurrency.Code} {plan.Installments[0].Amount.ToDecimal():N2} · "
                + $"Total a pagar: {ApplicationCurrency.Code} {plan.Loan.ExpectedTotal.ToDecimal():N2} · "
                + $"Interés total: {ApplicationCurrency.Code} {plan.Loan.TotalInterest.ToDecimal():N2} · "
                + $"Tasa mensual equivalente: {plan.EquivalentMonthlyRatePercent:N4} %";
        }
        catch (Exception)
        {
            LoanPreview = "Revisa los valores para calcular la vista previa.";
        }
    }

    private void ClearLoanForm()
    {
        LoanName = LoanInitialBalance = LoanMonthlyInterestPercent = LoanAgreedFinalAmount =
            LoanInstallmentCount = LoanDescription = string.Empty;
        LoanFirstDueDate = timeProvider.GetLocalNow().DateTime.Date;
        LoanPreview = "Completa los datos para ver el plan de cuotas.";
    }

    private void ResetDefinitionForm()
    {
        NameText = string.Empty;
        SelectedType = "Servicio";
        SelectedRecurrence = "Sin recurrencia";
        InitialDueDate = timeProvider.GetLocalNow().DateTime.Date;
        ExpectedAmountText = string.Empty;
        ObligationDescription = string.Empty;
        SelectedObligation = null;
        IsEditing = false;
    }

    private static bool IsPaid(Obligation obligation, AdministrationData data) => obligation.IsSettled
        || data.ObligationPayments.Where(item => item.ObligationId == obligation.Id).Sum(item => item.Amount.MinorUnits)
            >= obligation.ExpectedAmount.MinorUnits;
    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
    private static DateOnly RequiredDate(DateTime? value, string field) => value.HasValue
        ? DateOnly.FromDateTime(value.Value) : throw new ArgumentException($"La {field} es obligatoria.");
    private static Money ParseMoney(string value)
    {
        bool valid = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        return valid && amount > 0 ? Money.FromDecimal(amount)
            : throw new ArgumentException("El valor debe ser mayor que cero y tener máximo dos decimales.");
    }
    private static ObligationType ParseType(string value) => value switch
    {
        "Impuesto" => ObligationType.Tax,
        "Otra obligación" => ObligationType.OtherRecurring,
        _ => ObligationType.Service,
    };
    private static RecurrenceFrequency ParseRecurrence(string value) => value switch
    {
        "Mensual" => RecurrenceFrequency.Monthly,
        "Anual" => RecurrenceFrequency.Annual,
        _ => RecurrenceFrequency.None,
    };
    private static decimal ParseNonNegativeDecimal(string value, string field)
    {
        bool valid = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        return valid && amount >= 0 ? amount
            : throw new ArgumentException($"El {field} debe ser un número mayor o igual que cero.");
    }
    private static string LoanMethodName(LoanCalculationMethod method) => method switch
    {
        LoanCalculationMethod.MonthlyBalanceInterest => "Interés mensual sobre saldo",
        LoanCalculationMethod.AgreedFinalAmount => "Cantidad final acordada",
        _ => "Préstamo anterior",
    };
    private static string TypeName(ObligationType value) => value switch
    {
        ObligationType.Tax => "Impuesto",
        ObligationType.OtherRecurring => "Otra obligación",
        _ => "Servicio",
    };
    private static string RecurrenceName(RecurrenceFrequency value) => value switch
    {
        RecurrenceFrequency.Monthly => "Mensual",
        RecurrenceFrequency.Annual => "Anual",
        _ => "Sin recurrencia",
    };
}

public sealed record ObligationCatalogRow(Guid SeriesId, Obligation Definition, string Name, string Type,
    string Recurrence, string NextDueDate, string ExpectedAmount, string Description);
public sealed record ObligationPaymentListRow(string Date, string Obligation, string CoveredDueDate,
    string ActualAmount, string Description);
public sealed record ObligationSeriesOption(Guid SeriesId, string Display);
public sealed record LoanRow(Loan Loan, string Name, string InitialBalance, string PendingBalance,
    string Installment, string ExpectedTotal, string TotalInterest, string Method,
    string StartDate, string NextDueDate, string State, string Description);
public sealed record LoanInstallmentRow(LoanInstallment Installment, string Loan, int Number,
    string DueDate, string Amount, string Principal, string Interest, string PrincipalBalance,
    string State, string Description);
public sealed record LoanPaymentRow(string Date, string Loan, string Amount, string Description);
