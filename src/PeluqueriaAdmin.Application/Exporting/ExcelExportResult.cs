namespace PeluqueriaAdmin.Application.Exporting;

public sealed record ExcelExportResult(
    string FilePath,
    DateTime CutoffLocal,
    string Version,
    string CurrencyCode);
