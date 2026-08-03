using System.IO;
using System.Reflection;

namespace PeluqueriaAdmin.App.Tests;

public sealed class StartupDiagnosticsTests
{
    [Fact]
    public void StartupFailure_WritesCompleteDiagnosticOnlyInsideIsolatedDataRoot()
    {
        string temporaryRoot = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            Guid.NewGuid().ToString("N"));
        string? previousRoot = Environment.GetEnvironmentVariable(
            "PELUQUERIA_ADMIN_DATA_ROOT");

        try
        {
            Environment.SetEnvironmentVariable(
                "PELUQUERIA_ADMIN_DATA_ROOT",
                temporaryRoot);
            var rootCause = new InvalidOperationException(
                "Fallo de prueba Phase523");
            var exception = new ApplicationException(
                "Preparación interrumpida",
                rootCause);
            MethodInfo? discoveredMethod = typeof(PeluqueriaAdmin.App.App).GetMethod(
                "TryWriteStartupFailureLog",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(discoveredMethod);
            MethodInfo method = discoveredMethod;

            string logPath = Assert.IsType<string>(
                method.Invoke(null, [exception]));

            Assert.StartsWith(
                Path.GetFullPath(temporaryRoot),
                Path.GetFullPath(logPath),
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Logs", Directory.GetParent(logPath)?.Name);
            Assert.Matches(
                "^startup-error-[0-9]{8}-[0-9]{6}-[0-9]{3}\\.log$",
                Path.GetFileName(logPath));

            string diagnostic = File.ReadAllText(logPath);
            Assert.Contains("Fallo de prueba Phase523", diagnostic, StringComparison.Ordinal);
            Assert.Contains("Preparación interrumpida", diagnostic, StringComparison.Ordinal);
            Assert.Contains("Excepción raíz:", diagnostic, StringComparison.Ordinal);
            Assert.Contains("Excepción completa:", diagnostic, StringComparison.Ordinal);
            Assert.Contains(temporaryRoot, diagnostic, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "PELUQUERIA_ADMIN_DATA_ROOT",
                previousRoot);
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }
}
