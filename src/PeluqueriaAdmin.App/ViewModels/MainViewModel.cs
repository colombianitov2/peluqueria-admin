using System.Globalization;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed class MainViewModel
{
    public string FechaActual { get; } = DateTime.Today.ToString(
        "D",
        CultureInfo.GetCultureInfo("es-ES"));

    public string EstadoServiciosEImpuestos { get; } = "Sin datos registrados";

    public string EstadoPersonasConPagosPendientes { get; } = "Sin datos registrados";

    public string EstadoPuntoDeEquilibrio { get; } = "Sin calcular";
}
