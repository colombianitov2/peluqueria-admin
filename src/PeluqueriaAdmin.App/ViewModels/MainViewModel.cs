using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AdministrationService administrationService;
    private readonly GetSettingsUseCase getSettings;

    public MainViewModel(
        SettingsViewModel settings,
        AdministrationViewModel administration,
        AdministrationService administrationService,
        GetSettingsUseCase getSettings)
    {
        Settings = settings;
        Administration = administration;
        this.administrationService = administrationService;
        this.getSettings = getSettings;
    }

    public SettingsViewModel Settings { get; }

    public AdministrationViewModel Administration { get; }

    [ObservableProperty]
    private string fechaActual = DateTime.Today.ToString("D", CultureInfo.GetCultureInfo("es-ES"));

    [ObservableProperty]
    private string estadoServiciosEImpuestos = "Sin obligaciones pendientes";

    [ObservableProperty]
    private string estadoPersonasConPagosPendientes = "Sin personas con deuda";

    [ObservableProperty]
    private string estadoPuntoDeEquilibrio = "Sin faltante calculado";

    [ObservableProperty]
    private bool isHomeVisible = true;

    [ObservableProperty]
    private bool isSettingsVisible;

    [ObservableProperty]
    private bool isAdministrationVisible;

    [RelayCommand]
    private async Task ShowHomeAsync()
    {
        IsHomeVisible = true;
        IsSettingsVisible = false;
        IsAdministrationVisible = false;
        await RefreshHomeAsync();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        IsHomeVisible = false;
        IsSettingsVisible = true;
        IsAdministrationVisible = false;
    }

    [RelayCommand]
    private async Task ShowModuleAsync(string module)
    {
        IsHomeVisible = false;
        IsSettingsVisible = false;
        IsAdministrationVisible = true;
        await Administration.SelectModuleAsync(module);
    }

    public async Task RefreshHomeAsync()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        AdministrationData data = await administrationService.LoadAsync();
        SettingsDto settings = await getSettings.ExecuteAsync();
        FechaActual = DateTime.Today.ToString("D", CultureInfo.GetCultureInfo("es-ES"));

        string[] pendingObligations = data.Obligations
            .Where(item => item.Status(data.ObligationPayments, today) != ObligationStatus.Paid)
            .OrderBy(item => item.DueDate)
            .Select(item => $"{item.DueDate:yyyy-MM-dd} · {item.Name}")
            .ToArray();
        EstadoServiciosEImpuestos = pendingObligations.Length == 0
            ? "Sin obligaciones pendientes"
            : string.Join(Environment.NewLine, pendingObligations);

        string[] debts = data.LocalUsePeople.Select(person => new
        {
            person.Name,
            Debt = WeeklyChargeCalculator.CalculateDebt(
                    data.WeeklyCharges.Where(item => item.PersonId == person.Id),
                    data.LocalUsePayments.Where(item => item.PersonId == person.Id)),
        })
            .Where(item => item.Debt.MinorUnits > 0)
            .OrderBy(item => item.Name)
            .Select(item => $"{item.Name} · {settings.CurrencyCode} {item.Debt.ToDecimal():N2}")
            .ToArray();
        EstadoPersonasConPagosPendientes = debts.Length == 0
            ? "Sin personas con deuda"
            : string.Join(Environment.NewLine, debts);

        MonthlySummaryResult summary = MonthlySummaryCalculator.Calculate(
            AdministrationViewModel.BuildMonthlyInput(data, settings, YearMonth.From(today)),
            Percentage.FromPercent(settings.CollaboratorProfitPercent));
        EstadoPuntoDeEquilibrio = $"{settings.CurrencyCode} {summary.MissingMinorUnits / 100m:N2}";
    }
}
