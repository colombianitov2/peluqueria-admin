using System.IO;

namespace PeluqueriaAdmin.App.Tests;

internal static class RepositoryFiles
{
    internal static string Root { get; } = FindRoot();

    internal static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root, .. parts]));

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PeluqueriaAdmin.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
