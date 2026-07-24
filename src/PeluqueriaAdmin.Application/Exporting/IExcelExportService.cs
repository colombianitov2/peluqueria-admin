namespace PeluqueriaAdmin.Application.Exporting;

public interface IExcelExportService
{
    Task<ExcelExportResult> ExportAsync(CancellationToken cancellationToken = default);
}
