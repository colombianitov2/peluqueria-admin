using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.DataManagement;
using PeluqueriaAdmin.Application.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class SettingsViewModel(
    GetSettingsUseCase getSettings,
    SaveSettingsUseCase saveSettings,
    IDataManagementService dataManagement) : ObservableObject
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
    private string restorePath = string.Empty;

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

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task CreateBackupAsync()
    {
        await RunDataOperationAsync(
            dataManagement.CreateManualBackupAsync,
            path => $"Copia de seguridad creada correctamente:{Environment.NewLine}{path}",
            "No fue posible crear la copia de seguridad.");
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreBackupAsync()
    {
        string path = RestorePath.Trim();
        await RunDataOperationAsync(
            cancellationToken => dataManagement.RestoreAsync(path, cancellationToken),
            () => "La copia se restauró correctamente. Reinicia la aplicación para trabajar con los datos restaurados.",
            "No fue posible restaurar la copia. La base de datos anterior se conservó.");
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task ExportDataAsync()
    {
        await RunDataOperationAsync(
            dataManagement.ExportAsync,
            files => $"Se exportaron {files.Count} archivos CSV en:{Environment.NewLine}{dataManagement.ExportsDirectory}",
            "No fue posible exportar los datos.");
    }

    [RelayCommand]
    private void OpenBackups() => OpenDirectory(dataManagement.BackupsDirectory);

    [RelayCommand]
    private void OpenExports() => OpenDirectory(dataManagement.ExportsDirectory);

    private bool CanSave() => !IsBusy;

    private bool CanRestore() => !IsBusy && !string.IsNullOrWhiteSpace(RestorePath);

    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CreateBackupCommand.NotifyCanExecuteChanged();
        RestoreBackupCommand.NotifyCanExecuteChanged();
        ExportDataCommand.NotifyCanExecuteChanged();
    }

    partial void OnRestorePathChanged(string value) => RestoreBackupCommand.NotifyCanExecuteChanged();

    private async Task RunDataOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<T, string> successMessage,
        string errorMessage)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        IsError = false;
        try
        {
            T result = await operation(CancellationToken.None);
            StatusMessage = successMessage(result);
        }
        catch (Exception exception)
        {
            SetDataError(errorMessage, exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunDataOperationAsync(
        Func<CancellationToken, Task> operation,
        Func<string> successMessage,
        string errorMessage)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        IsError = false;
        try
        {
            await operation(CancellationToken.None);
            StatusMessage = successMessage();
        }
        catch (Exception exception)
        {
            SetDataError(errorMessage, exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetDataError(string message, Exception exception)
    {
        StatusMessage = message;
#if DEBUG
        StatusMessage += $"{Environment.NewLine}{exception.Message}";
#else
        _ = exception;
#endif
        IsError = true;
    }

    private void OpenDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible abrir la carpeta.", exception);
        }
    }

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
