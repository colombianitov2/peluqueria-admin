namespace PeluqueriaAdmin.Infrastructure.Storage;

public sealed class ApplicationPaths
{
    private const string ApplicationDirectoryName = "PeluqueriaAdmin";
    private const string DatabaseFileName = "peluqueria-admin.db";

    private ApplicationPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        DataDirectory = Path.Combine(rootDirectory, "Data");
        BackupsDirectory = Path.Combine(rootDirectory, "Backups");
        LogsDirectory = Path.Combine(rootDirectory, "Logs");
        DatabaseFilePath = Path.Combine(DataDirectory, DatabaseFileName);
    }

    public string RootDirectory { get; }

    public string DataDirectory { get; }

    public string BackupsDirectory { get; }

    public string LogsDirectory { get; }

    public string DatabaseFilePath { get; }

    public static ApplicationPaths ForCurrentUser()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("Windows no proporcionó una carpeta LocalAppData válida.");
        }

        return FromRoot(Path.Combine(localApplicationData, ApplicationDirectoryName));
    }

    public static ApplicationPaths FromRoot(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return new ApplicationPaths(Path.GetFullPath(rootDirectory));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
