using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class SettingsViewModel(
    GetSettingsUseCase getSettings,
    SaveSettingsUseCase saveSettings) : ObservableObject
{
    [ObservableProperty]
    private string weeklyUsageFee = string.Empty;

    [ObservableProperty]
    private string collaboratorProfitPercent = string.Empty;

    [ObservableProperty]
    private string optionalSuppliesMonthlyBudget = string.Empty;

    [ObservableProperty]
    private string totalChairs = string.Empty;

    [ObservableProperty]
    private string currencyCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isError;

    [ObservableProperty]
    private bool isBusy;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            SettingsDto settings = await getSettings.ExecuteAsync(cancellationToken);
            Apply(settings);
            StatusMessage = string.Empty;
            IsError = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        IsError = false;

        if (!TryCreateRequest(out SaveSettingsRequest request, out string validationMessage))
        {
            StatusMessage = validationMessage;
            IsError = true;
            return;
        }

        IsBusy = true;
        try
        {
            SettingsDto settings = await saveSettings.ExecuteAsync(request);
            Apply(settings);
            StatusMessage = "Los ajustes se guardaron correctamente.";
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "No fue posible guardar los ajustes. Verifica los datos e inténtalo de nuevo.";
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = "No fue posible guardar los ajustes. Los datos anteriores se conservaron.";
#if DEBUG
            StatusMessage += $"{Environment.NewLine}{exception}";
#else
            _ = exception;
#endif
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSave() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    private bool TryCreateRequest(
        out SaveSettingsRequest request,
        out string validationMessage)
    {
        var errors = new List<string>();

        bool weeklyFeeIsValid = TryParseDecimal(WeeklyUsageFee, out decimal weeklyFee);
        if (!weeklyFeeIsValid)
        {
            errors.Add("El valor semanal debe ser un número válido.");
        }

        bool profitIsValid = TryParseDecimal(CollaboratorProfitPercent, out decimal profit);
        if (!profitIsValid)
        {
            errors.Add("La ganancia de colaboradores debe ser un número válido.");
        }

        bool budgetIsValid = TryParseDecimal(OptionalSuppliesMonthlyBudget, out decimal budget);
        if (!budgetIsValid)
        {
            errors.Add("El presupuesto mensual debe ser un número válido.");
        }

        bool chairsAreValid = int.TryParse(
            TotalChairs,
            NumberStyles.None,
            CultureInfo.CurrentCulture,
            out int chairs);
        if (!chairsAreValid)
        {
            errors.Add("La cantidad total de sillas debe ser un número entero.");
        }

        if (string.IsNullOrWhiteSpace(CurrencyCode))
        {
            errors.Add("El código de moneda es obligatorio.");
        }

        if (errors.Count > 0)
        {
            request = null!;
            validationMessage = string.Join(Environment.NewLine, errors);
            return false;
        }

        request = new SaveSettingsRequest(weeklyFee, profit, budget, chairs, CurrencyCode);
        validationMessage = string.Empty;
        return true;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint;

        return decimal.TryParse(value, styles, CultureInfo.CurrentCulture, out result)
            || decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out result);
    }

    private void Apply(SettingsDto settings)
    {
        WeeklyUsageFee = settings.WeeklyUsageFee.ToString("0.00", CultureInfo.CurrentCulture);
        CollaboratorProfitPercent = settings.CollaboratorProfitPercent.ToString("0.00", CultureInfo.CurrentCulture);
        OptionalSuppliesMonthlyBudget = settings.OptionalSuppliesMonthlyBudget.ToString("0.00", CultureInfo.CurrentCulture);
        TotalChairs = settings.TotalChairs.ToString(CultureInfo.CurrentCulture);
        CurrencyCode = settings.CurrencyCode;
    }
}
