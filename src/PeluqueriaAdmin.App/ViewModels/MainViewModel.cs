using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(SettingsViewModel settings)
    {
        Settings = settings;
    }

    public SettingsViewModel Settings { get; }

    public string FechaActual { get; } = DateTime.Today.ToString(
        "D",
        CultureInfo.GetCultureInfo("es-ES"));

    public string EstadoServiciosEImpuestos { get; } = "Sin datos registrados";

    public string EstadoPersonasConPagosPendientes { get; } = "Sin datos registrados";

    public string EstadoPuntoDeEquilibrio { get; } = "Sin calcular";

    [ObservableProperty]
    private bool isHomeVisible = true;

    [ObservableProperty]
    private bool isSettingsVisible;

    [RelayCommand]
    private void ShowHome()
    {
        IsHomeVisible = true;
        IsSettingsVisible = false;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        IsHomeVisible = false;
        IsSettingsVisible = true;
    }
}
