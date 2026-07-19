using ClosedXML.Excel;

namespace PeluqueriaAdmin.Infrastructure.Exporting;

public interface IExcelWorkbookWriter
{
    void Save(XLWorkbook workbook, string path);
}

public sealed class ClosedXmlWorkbookWriter : IExcelWorkbookWriter
{
    public void Save(XLWorkbook workbook, string path) => workbook.SaveAs(path);
}
