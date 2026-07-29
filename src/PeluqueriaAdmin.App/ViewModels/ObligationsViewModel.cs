using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class ObligationsViewModel(AdministrationService service, TimeProvider timeProvider) : ObservableObject
{
    public ObservableCollection<string> TypeOptions { get; } =
        ["Servicio", "Impuesto", "Crédito", "Otra obligación"];
    public ObservableCollection<string> RecurrenceOptions { get; } =
        ["Sin recurrencia", "Semanal", "Mensual", "Anual"];
    public ObservableCollection<ObligationCatalogRow> Obligations { get; } = [];
    public ObservableCollection<ObligationPaymentListRow> Payments { get; } = [];
    public ObservableCollection<ObligationSeriesOption> PaymentOptions { get; } = [];
    public ObservableCollection<LoanRow> Loans { get; } = [];
    public ObservableCollection<LoanInstallmentRow> LoanInstallments { get; } = [];
    public ObservableCollection<LoanPaymentRow> LoanPayments { get; } = [];
    public ObservableCollection<LoanPreviewInstallmentRow> LoanPreviewInstallments { get; } = [];
    public ObservableCollection<string> LoanCalculationMethodOptions { get; } =
        ["Interés mensual sobre saldo", "Interés fijo sobre capital inicial", "Cantidad final acordada"];

    [ObservableProperty] private bool isAddMode = true;
    [ObservableProperty] private bool isLoanMode;
    [ObservableProperty] private string nameText = string.Empty;
    [ObservableProperty] private string selectedType = string.Empty;
    [ObservableProperty] private string selectedRecurrence = string.Empty;
    [ObservableProperty] private DateTime? initialDueDate;
    [ObservableProperty] private string expectedAmountText = string.Empty;
    [ObservableProperty] private string obligationDescription = string.Empty;
    [ObservableProperty] private ObligationCatalogRow? selectedObligation;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private bool confirmDelete;
    [ObservableProperty] private ObligationSeriesOption? selectedPaymentOption;
    [ObservableProperty] private DateTime? paymentDate;
    [ObservableProperty] private string paymentAmountText = string.Empty;
    [ObservableProperty] private string paymentDescription = string.Empty;
    [ObservableProperty] private ObligationPaymentListRow? selectedObligationPayment;
    [ObservableProperty] private bool isEditingObligationPayment;
    [ObservableProperty] private bool confirmObligationPaymentDelete;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;

    [ObservableProperty] private string loanName = string.Empty;
    [ObservableProperty] private string loanInitialBalance = string.Empty;
    [ObservableProperty] private string selectedLoanCalculationMethod = string.Empty;
    [ObservableProperty] private string loanMonthlyInterestPercent = string.Empty;
    [ObservableProperty] private string loanAgreedFinalAmount = string.Empty;
    [ObservableProperty] private string loanInstallmentCount = string.Empty;
    [ObservableProperty] private DateTime? loanFirstDueDate;
    [ObservableProperty] private string loanDescription = string.Empty;
    [ObservableProperty] private string loanPreview = "Completa los datos para ver el plan de cuotas.";
    [ObservableProperty] private string loanPreviewError = string.Empty;
    [ObservableProperty] private bool showMonthlyInterest = true;
    [ObservableProperty] private bool showAgreedFinalAmount;
    [ObservableProperty] private LoanRow? selectedLoan;
    [ObservableProperty] private bool isEditingLoan;
    [ObservableProperty] private bool canEditLoanTerms = true;
    [ObservableProperty] private bool confirmLoanDelete;
    [ObservableProperty] private DateTime? loanPaymentDate;
    [ObservableProperty] private string loanPaymentAmount = string.Empty;
    [ObservableProperty] private string loanPaymentDescription = string.Empty;
    [ObservableProperty] private LoanPaymentRow? selectedLoanPayment;
    [ObservableProperty] private bool isEditingLoanPayment;
    [ObservableProperty] private bool confirmLoanPaymentDelete;

    public bool IsPaymentMode => !IsAddMode && !IsLoanMode;

    public async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        DateOnly today = Today();
        AdministrationData data = await service.GenerateScheduledRecordsAsync(
            YearMonth.From(today).LastDay);
        Guid? selectedSeries = SelectedObligation?.SeriesId;
        Guid? selectedObligationPaymentId = SelectedObligationPayment?.Payment.Id;
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
                payment,
                occurrence?.SeriesId ?? Guid.Empty,
                payment.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                occurrence?.Name ?? "Obligación eliminada",
                occurrence?.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                $"{ApplicationCurrency.Code} {payment.Amount.ToDecimal():N2}",
                payment.Description ?? string.Empty));
        }
        SelectedObligationPayment = selectedObligationPaymentId.HasValue
            ? Payments.SingleOrDefault(item => item.Payment.Id == selectedObligationPaymentId.Value)
            : null;
        SelectedObligation = selectedSeries.HasValue ? Obligations.SingleOrDefault(item => item.SeriesId == selectedSeries) : null;
        Loans.Clear();
        foreach (Loan loan in data.Loans.OrderBy(item => item.NextDueDate))
        {
            LoanInstallment[] installments = data.LoanInstallments
                .Where(item => item.LoanId == loan.Id)
                .OrderBy(item => item.Number)
                .ToArray();
            long paidMinorUnits = data.LoanPayments
                .Where(item => item.LoanId == loan.Id)
                .Sum(item => item.Amount.MinorUnits);
            Loans.Add(new LoanRow(loan, loan.Name, $"{ApplicationCurrency.Code} {loan.InitialBalance.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.PendingBalance.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.UsualInstallment.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.ExpectedTotal.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.ExpectedTotal.ToDecimal():N2}",
                $"{ApplicationCurrency.Code} {loan.TotalInterest.ToDecimal():N2}",
                LoanMethodName(loan.CalculationMethod),
                loan.InstallmentCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                "Mensual",
                loan.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                installments.LastOrDefault()?.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    ?? loan.NextDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                $"{ApplicationCurrency.Code} {Money.FromMinorUnits(paidMinorUnits).ToDecimal():N2}",
                loan.NextDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                loan.IsPaid ? "Pagado" : "Pendiente", loan.Description ?? string.Empty));
        }
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
            LoanPayments.Add(new LoanPaymentRow(payment, payment.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
            LoanPlan plan = BuildLoanPlan(
                LoanName, principal, count, firstDueDate, utcNow, LoanDescription);
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
    private async Task EditSelectedLoanAsync()
    {
        if (SelectedLoan is null)
        {
            StatusMessage = "Selecciona un préstamo para editar.";
            IsError = true;
            return;
        }

        AdministrationData data = await service.LoadAsync();
        Loan loan = SelectedLoan.Loan;
        bool hasPayments = data.LoanPayments.Any(item => item.LoanId == loan.Id);
        IsEditingLoan = true;
        CanEditLoanTerms = !hasPayments && loan.CalculationMethod != LoanCalculationMethod.Legacy;
        LoanName = loan.Name;
        LoanDescription = loan.Description ?? string.Empty;
        LoanInitialBalance = loan.InitialBalance.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        SelectedLoanCalculationMethod = LoanMethodName(loan.CalculationMethod);
        LoanMonthlyInterestPercent = (loan.MonthlyInterestBasisPoints / 100m)
            .ToString("0.####", CultureInfo.CurrentCulture);
        LoanAgreedFinalAmount = loan.ExpectedTotal.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        LoanInstallmentCount = loan.InstallmentCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        LoanFirstDueDate = loan.StartDate.ToDateTime(TimeOnly.MinValue);
        StatusMessage = hasPayments
            ? "Este préstamo ya tiene pagos: solo puedes editar el nombre y la descripción."
            : "Edición activa. Los cambios financieros regenerarán el calendario antes del primer pago.";
        IsError = false;
        UpdateLoanPreview();
    }

    [RelayCommand]
    private async Task SaveLoanEditAsync()
    {
        try
        {
            if (!IsEditingLoan || SelectedLoan is null)
                throw new InvalidOperationException("Selecciona Editar préstamo antes de guardar.");
            Guid loanId = SelectedLoan.Loan.Id;
            if (!CanEditLoanTerms)
            {
                await service.UpdateLoanMetadataAsync(loanId, LoanName, LoanDescription);
            }
            else
            {
                int count = int.TryParse(LoanInstallmentCount, out int parsed) && parsed > 0
                    ? parsed
                    : throw new ArgumentException("La cantidad de cuotas debe ser un entero positivo.");
                LoanPlan replacement = BuildLoanPlan(
                    LoanName,
                    ParseMoney(LoanInitialBalance),
                    count,
                    RequiredDate(LoanFirstDueDate, "fecha de la primera cuota"),
                    timeProvider.GetUtcNow().UtcDateTime,
                    LoanDescription);
                await service.ReplaceLoanPlanAsync(loanId, replacement);
            }
            ClearLoanForm();
            IsEditingLoan = false;
            CanEditLoanTerms = true;
            StatusMessage = "El préstamo se actualizó conservando la trazabilidad.";
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
    private async Task DeleteSelectedLoanAsync()
    {
        try
        {
            if (SelectedLoan is null || !ConfirmLoanDelete)
                throw new InvalidOperationException("Selecciona un préstamo y confirma la eliminación.");
            await service.DeleteLoanAsync(SelectedLoan.Loan.Id);
            ClearLoanForm();
            ConfirmLoanDelete = false;
            IsEditingLoan = false;
            StatusMessage = "El préstamo se eliminó lógicamente y quedó en el historial eliminado.";
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
    private void EditSelectedLoanPayment()
    {
        if (SelectedLoanPayment is null)
        {
            StatusMessage = "Selecciona un pago de préstamo para editar.";
            IsError = true;
            return;
        }
        IsEditingLoanPayment = true;
        LoanPaymentDate = SelectedLoanPayment.Payment.Date.ToDateTime(TimeOnly.MinValue);
        LoanPaymentAmount = SelectedLoanPayment.Payment.Amount.ToDecimal()
            .ToString("0.00", CultureInfo.CurrentCulture);
        LoanPaymentDescription = SelectedLoanPayment.Payment.Description ?? string.Empty;
        StatusMessage = "Edición del pago activa. Las cuotas programadas conservan su valor exacto.";
        IsError = false;
    }

    [RelayCommand]
    private async Task SaveLoanPaymentEditAsync()
    {
        try
        {
            if (!IsEditingLoanPayment || SelectedLoanPayment is null)
                throw new InvalidOperationException("Selecciona Editar pago antes de guardar.");
            await service.UpdateLoanPaymentAsync(
                SelectedLoanPayment.Payment.Id,
                RequiredDate(LoanPaymentDate, "fecha de pago"),
                ParseMoney(LoanPaymentAmount),
                LoanPaymentDescription);
            IsEditingLoanPayment = false;
            LoanPaymentAmount = LoanPaymentDescription = string.Empty;
            StatusMessage = "El pago se corrigió y el saldo del préstamo se recalculó.";
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
    private async Task DeleteSelectedLoanPaymentAsync()
    {
        try
        {
            if (SelectedLoanPayment is null || !ConfirmLoanPaymentDelete)
                throw new InvalidOperationException("Selecciona un pago y confirma la eliminación.");
            await service.DeleteLoanPaymentAsync(SelectedLoanPayment.Payment.Id);
            IsEditingLoanPayment = false;
            ConfirmLoanPaymentDelete = false;
            LoanPaymentAmount = LoanPaymentDescription = string.Empty;
            StatusMessage = "El pago se eliminó lógicamente y el préstamo se recalculó.";
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
            DateOnly today = Today();
            await service.AddObligationAsync(obligation, YearMonth.From(today).LastDay);
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
            PaymentDate = null;
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

    [RelayCommand(CanExecute = nameof(CanEditObligationPayment))]
    private void EditSelectedObligationPayment()
    {
        if (SelectedObligationPayment is null) return;
        SelectedPaymentOption = PaymentOptions.SingleOrDefault(item =>
            item.SeriesId == SelectedObligationPayment.SeriesId);
        PaymentDate = SelectedObligationPayment.Payment.Date.ToDateTime(TimeOnly.MinValue);
        PaymentAmountText = SelectedObligationPayment.Payment.Amount.ToDecimal()
            .ToString("0.00", CultureInfo.CurrentCulture);
        PaymentDescription = SelectedObligationPayment.Payment.Description ?? string.Empty;
        IsEditingObligationPayment = true;
        StatusMessage = "Edición del pago activa.";
        IsError = false;
    }

    [RelayCommand(CanExecute = nameof(CanSaveObligationPayment))]
    private async Task SaveObligationPaymentAsync()
    {
        if (SelectedObligationPayment is null || SelectedPaymentOption is null) return;
        try
        {
            Guid paymentId = SelectedObligationPayment.Payment.Id;
            await service.UpdateObligationPaymentAsync(
                paymentId,
                SelectedPaymentOption.SeriesId,
                RequiredDate(PaymentDate, "fecha de pago"),
                ParseMoney(PaymentAmountText),
                PaymentDescription);
            IsEditingObligationPayment = false;
            await RefreshAsync();
            SelectedObligationPayment = Payments.SingleOrDefault(item => item.Payment.Id == paymentId);
            StatusMessage = "El pago se actualizó y todos los saldos se recalcularon.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteObligationPayment))]
    private async Task DeleteSelectedObligationPaymentAsync()
    {
        if (SelectedObligationPayment is null || !ConfirmObligationPaymentDelete) return;
        try
        {
            await service.DeleteObligationPaymentAsync(SelectedObligationPayment.Payment.Id);
            ConfirmObligationPaymentDelete = false;
            IsEditingObligationPayment = false;
            await RefreshAsync();
            StatusMessage = "El pago se eliminó lógicamente y el vencimiento volvió a quedar pendiente.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    private bool CanEditObligationPayment() => SelectedObligationPayment is not null;
    private bool CanSaveObligationPayment() =>
        IsEditingObligationPayment && SelectedObligationPayment is not null;
    private bool CanDeleteObligationPayment() =>
        SelectedObligationPayment is not null && ConfirmObligationPaymentDelete;

    public Task FlushPendingAsync() => Task.CompletedTask;

    partial void OnIsAddModeChanged(bool value) => OnPropertyChanged(nameof(IsPaymentMode));
    partial void OnIsLoanModeChanged(bool value) => OnPropertyChanged(nameof(IsPaymentMode));
    partial void OnSelectedLoanCalculationMethodChanged(string value)
    {
        ShowAgreedFinalAmount = value == "Cantidad final acordada";
        ShowMonthlyInterest = !ShowAgreedFinalAmount;
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
    partial void OnSelectedObligationPaymentChanged(ObligationPaymentListRow? value)
    {
        EditSelectedObligationPaymentCommand.NotifyCanExecuteChanged();
        SaveObligationPaymentCommand.NotifyCanExecuteChanged();
        DeleteSelectedObligationPaymentCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsEditingObligationPaymentChanged(bool value) =>
        SaveObligationPaymentCommand.NotifyCanExecuteChanged();
    partial void OnConfirmObligationPaymentDeleteChanged(bool value) =>
        DeleteSelectedObligationPaymentCommand.NotifyCanExecuteChanged();

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
        LoanPreviewInstallments.Clear();
        LoanPreviewError = string.Empty;
        try
        {
            if (!int.TryParse(LoanInstallmentCount, out int count) || count <= 0
                || !LoanFirstDueDate.HasValue || string.IsNullOrWhiteSpace(LoanInitialBalance))
            {
                LoanPreview = "Completa principal, cantidad de cuotas y primera fecha de pago.";
                return;
            }
            LoanPlan plan = BuildLoanPlan(
                string.IsNullOrWhiteSpace(LoanName) ? "Vista previa" : LoanName,
                ParseMoney(LoanInitialBalance),
                count,
                DateOnly.FromDateTime(LoanFirstDueDate.Value),
                timeProvider.GetUtcNow().UtcDateTime);
            LoanPreview =
                $"Capital: {ApplicationCurrency.Code} {plan.Loan.InitialBalance.ToDecimal():N2} · "
                + $"Total a pagar: {ApplicationCurrency.Code} {plan.Loan.ExpectedTotal.ToDecimal():N2} · "
                + $"Interés total: {ApplicationCurrency.Code} {plan.Loan.TotalInterest.ToDecimal():N2} · "
                + $"Tasa mensual equivalente: {plan.EquivalentMonthlyRatePercent:N4} % · "
                + $"Primera cuota: {ApplicationCurrency.Code} {plan.Installments[0].Amount.ToDecimal():N2} · "
                + $"Última cuota: {ApplicationCurrency.Code} {plan.Installments[^1].Amount.ToDecimal():N2}.";
            foreach (LoanInstallment installment in plan.Installments)
            {
                LoanPreviewInstallments.Add(new LoanPreviewInstallmentRow(
                    installment.Number,
                    installment.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    $"{ApplicationCurrency.Code} {installment.Amount.ToDecimal():N2}",
                    $"{ApplicationCurrency.Code} {installment.Principal.ToDecimal():N2}",
                    $"{ApplicationCurrency.Code} {installment.Interest.ToDecimal():N2}",
                    $"{ApplicationCurrency.Code} {installment.PrincipalBalanceAfter.ToDecimal():N2}"));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            LoanPreview = "No se pudo calcular el plan.";
            LoanPreviewError = exception.Message;
        }
    }

    private LoanPlan BuildLoanPlan(
        string name,
        Money principal,
        int count,
        DateOnly firstDueDate,
        DateTime utcNow,
        string? description = null) => SelectedLoanCalculationMethod switch
        {
            "Cantidad final acordada" => LoanCalculator.AgreedFinalAmount(
                name, principal, ParseMoney(LoanAgreedFinalAmount), count,
                firstDueDate, utcNow, description),
            "Interés fijo sobre capital inicial" => LoanCalculator.FixedInterestOnInitialPrincipal(
                name, principal, ParseNonNegativeDecimal(LoanMonthlyInterestPercent, "interés mensual"),
                count, firstDueDate, utcNow, description),
            "Interés mensual sobre saldo" => LoanCalculator.MonthlyBalanceInterest(
                name, principal, ParseNonNegativeDecimal(LoanMonthlyInterestPercent, "interés mensual"),
                count, firstDueDate, utcNow, description),
            _ => throw new ArgumentException("Selecciona el método de cálculo del préstamo."),
        };

    private void ClearLoanForm()
    {
        LoanName = LoanInitialBalance = LoanMonthlyInterestPercent = LoanAgreedFinalAmount =
            LoanInstallmentCount = LoanDescription = string.Empty;
        SelectedLoanCalculationMethod = string.Empty;
        LoanFirstDueDate = null;
        LoanPreview = "Completa los datos para ver el plan de cuotas.";
        LoanPreviewError = string.Empty;
        LoanPreviewInstallments.Clear();
    }

    private void ResetDefinitionForm()
    {
        NameText = string.Empty;
        SelectedType = string.Empty;
        SelectedRecurrence = string.Empty;
        InitialDueDate = null;
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
        "Crédito" => ObligationType.Credit,
        "Otra obligación" => ObligationType.OtherRecurring,
        "Servicio" => ObligationType.Service,
        _ => throw new ArgumentException("Selecciona el tipo de obligación."),
    };
    private static RecurrenceFrequency ParseRecurrence(string value) => value switch
    {
        "Semanal" => RecurrenceFrequency.Weekly,
        "Mensual" => RecurrenceFrequency.Monthly,
        "Anual" => RecurrenceFrequency.Annual,
        "Sin recurrencia" => RecurrenceFrequency.None,
        _ => throw new ArgumentException("Selecciona la recurrencia."),
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
        LoanCalculationMethod.FixedInterestOnInitialPrincipal => "Interés fijo sobre capital inicial",
        LoanCalculationMethod.AgreedFinalAmount => "Cantidad final acordada",
        _ => "Préstamo anterior",
    };
    private static string TypeName(ObligationType value) => value switch
    {
        ObligationType.Tax => "Impuesto",
        ObligationType.Credit => "Crédito",
        ObligationType.OtherRecurring => "Otra obligación",
        _ => "Servicio",
    };
    private static string RecurrenceName(RecurrenceFrequency value) => value switch
    {
        RecurrenceFrequency.Weekly => "Semanal",
        RecurrenceFrequency.Monthly => "Mensual",
        RecurrenceFrequency.Annual => "Anual",
        _ => "Sin recurrencia",
    };
}

public sealed record ObligationCatalogRow(Guid SeriesId, Obligation Definition, string Name, string Type,
    string Recurrence, string NextDueDate, string ExpectedAmount, string Description);
public sealed record ObligationPaymentListRow(
    ObligationPayment Payment,
    Guid SeriesId,
    string Date,
    string Obligation,
    string CoveredDueDate,
    string ActualAmount,
    string Description);
public sealed record ObligationSeriesOption(Guid SeriesId, string Display);
public sealed record LoanRow(
    Loan Loan,
    string Name,
    string InitialBalance,
    string PendingBalance,
    string Installment,
    string ExpectedTotal,
    string AgreedFinalAmount,
    string TotalInterest,
    string Method,
    string InstallmentCount,
    string Periodicity,
    string StartDate,
    string EndDate,
    string TotalPaid,
    string NextDueDate,
    string State,
    string Description);
public sealed record LoanInstallmentRow(LoanInstallment Installment, string Loan, int Number,
    string DueDate, string Amount, string Principal, string Interest, string PrincipalBalance,
    string State, string Description);
public sealed record LoanPaymentRow(LoanPayment Payment, string Date, string Loan, string Amount, string Description);
public sealed record LoanPreviewInstallmentRow(int Number, string DueDate, string Amount,
    string Principal, string Interest, string PrincipalBalance);
