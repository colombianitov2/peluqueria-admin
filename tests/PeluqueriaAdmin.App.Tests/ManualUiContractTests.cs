using System.Text.RegularExpressions;

namespace PeluqueriaAdmin.App.Tests;

public sealed class ManualUiContractTests
{
    [Fact]
    public void Manual_IsRegisteredBelowNotesAndNavigatesWithoutPersistence()
    {
        string main = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "MainViewModel.cs");
        string window = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "MainWindow.xaml");
        string app = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "App.xaml.cs");
        string viewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "ManualViewModel.cs");

        int notesPosition = main.IndexOf("new(\"Notas\", false)", StringComparison.Ordinal);
        int manualPosition = main.IndexOf("new(\"Manual\", false)", StringComparison.Ordinal);
        Assert.True(notesPosition >= 0 && manualPosition > notesPosition);
        Assert.Contains("public ManualViewModel Manual { get; }", main, StringComparison.Ordinal);
        Assert.Contains("CurrentPage = Manual;", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Manual.LoadAsync", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Manual.FlushPendingAsync", main, StringComparison.Ordinal);

        Assert.Contains("DataType=\"{x:Type viewModels:ManualViewModel}\"", window, StringComparison.Ordinal);
        Assert.Contains("<views:ManualView />", window, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<ManualViewModel>();", app, StringComparison.Ordinal);

        Assert.DoesNotContain("Repository", viewModel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", viewModel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Save", viewModel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Manual_IsScrollableReadOnlyAndCoversEveryApprovedTopic()
    {
        string view = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "ManualView.xaml");

        Assert.Contains("FlowDocumentScrollViewer", view, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", view, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", view, StringComparison.Ordinal);
        Assert.Contains("IsToolBarVisible=\"False\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBox", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button", view, StringComparison.Ordinal);
        Assert.DoesNotContain("{Binding", view, StringComparison.Ordinal);

        MatchCollection headings = Regex.Matches(
            view,
            "Style=\"\\{StaticResource SectionTitle\\}\">(\\d+)\\.",
            RegexOptions.CultureInvariant);
        Assert.Equal(21, headings.Count);
        Assert.Equal(
            Enumerable.Range(1, 21),
            headings.Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)));

        string[] requiredTopics =
        [
            "Inicio",
            "Uso del local",
            "Colaboradores",
            "Ventas",
            "Inventario",
            "Agregar al inventario",
            "Lista mensual de compra",
            "Otros ingresos, Gastos e Imprevistos",
            "Obligaciones y créditos",
            "Sin recurrencia, Semanal, Mensual y Anual",
            "Préstamos",
            "Mantenimiento",
            "Resumen mensual",
            "Balance anual",
            "Ajustes",
            "Notas y Manual",
            "Copias de seguridad y restauración",
            "Exportación completa a Excel",
            "Actualizaciones mediante GitHub",
            "Solución de problemas y glosario",
            "No desinstale el programa para actualizarlo",
            "copia completa y restaurable de SQLite",
            "Excel es una fotografía para consulta",
            "lupa de búsqueda",
            "Agregar, Editar, Guardar y Eliminar",
            "Servicio, Impuesto, Crédito y Otra obligación",
        ];
        Assert.All(requiredTopics, topic => Assert.Contains(topic, view, StringComparison.Ordinal));
    }

    [Fact]
    public void StartupAndHome_GenerateRecurringRecordsThroughCurrentMonth()
    {
        string app = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "App.xaml.cs");
        string main = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "MainViewModel.cs");

        Assert.Matches(
            new Regex(
                "GenerateScheduledRecordsAsync\\(YearMonth\\.From\\(today\\)\\.LastDay\\)",
                RegexOptions.CultureInvariant),
            app);
        Assert.Matches(
            new Regex(
                "GenerateScheduledRecordsAsync\\(\\s*YearMonth\\.From\\(today\\)\\.LastDay\\)",
                RegexOptions.CultureInvariant),
            main);
    }

    [Fact]
    public void App_RejectsASecondInstanceBeforeConstructingTheApplication()
    {
        string app = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "App.xaml.cs");

        Assert.Contains(
            "SingleInstanceMutexName = @\"Local\\Colombianito.PeluqueriaAdmin\"",
            app,
            StringComparison.Ordinal);
        Assert.Contains(
            "new Mutex(true, SingleInstanceMutexName, out bool createdNew)",
            app,
            StringComparison.Ordinal);
        Assert.Contains("Peluquería Admin ya está abierta", app, StringComparison.Ordinal);
        Assert.Contains("singleInstanceMutex.ReleaseMutex();", app, StringComparison.Ordinal);
        Assert.Contains("singleInstanceMutex.Dispose();", app, StringComparison.Ordinal);

        int mutexPosition = app.IndexOf(
            "new Mutex(true, SingleInstanceMutexName, out bool createdNew)",
            StringComparison.Ordinal);
        int applicationPosition = app.IndexOf("var app = new App();", StringComparison.Ordinal);
        Assert.True(mutexPosition >= 0 && applicationPosition > mutexPosition);
    }
}
