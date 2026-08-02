namespace PeluqueriaAdmin.App.Tests;

public sealed class ReleaseVersionContractTests
{
    [Fact]
    public void Phase52_VersionMetadataAndDocumentationAreConsistent()
    {
        string project = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "PeluqueriaAdmin.App.csproj");
        string manual = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "ManualView.xaml");
        string externalManual = RepositoryFiles.Read("docs", "MANUAL_USUARIO.md");
        string releaseNotes = RepositoryFiles.Read("docs", "NOTAS_VERSION_0.2.0-alpha.3.md");

        Assert.Contains("<Version>0.2.0-alpha.3</Version>", project, StringComparison.Ordinal);
        Assert.Contains("<AssemblyVersion>0.2.0.0</AssemblyVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<FileVersion>0.2.0.3</FileVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<InformationalVersion>0.2.0-alpha.3</InformationalVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>", project, StringComparison.Ordinal);
        Assert.Contains("Versión documentada: 0.2.0-alpha.3", manual, StringComparison.Ordinal);
        Assert.Contains("Versión documentada: **0.2.0-alpha.3**", externalManual, StringComparison.Ordinal);
        Assert.Contains("Versión preliminar pública", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("no está firmado digitalmente", releaseNotes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "PeluqueriaAdmin-Setup-v0.2.0-alpha.3.exe",
            releaseNotes,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Phase52_ReleaseAndUpdaterUseThePublicRepositoryWithoutEmbeddedCredentials()
    {
        string updater = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Updates", "VelopackUpdateService.cs");
        string workflow = RepositoryFiles.Read(".github", "workflows", "release.yml");

        Assert.Contains(
            "https://github.com/colombianitov2/peluqueria-admin",
            updater,
            StringComparison.Ordinal);
        Assert.Contains("--repoUrl", workflow, StringComparison.Ordinal);
        Assert.Contains("https://github.com/${{ github.repository }}", workflow, StringComparison.Ordinal);
        Assert.Contains("PeluqueriaAdmin-Setup-v$version.exe", workflow, StringComparison.Ordinal);
        Assert.Contains("PeluqueriaAdmin-Portable-v$version-win-x64.zip", workflow, StringComparison.Ordinal);
        Assert.Contains("-p:Version=${{ steps.version.outputs.version }}", workflow, StringComparison.Ordinal);
        Assert.Contains("-p:InformationalVersion=${{ steps.version.outputs.version }}", workflow, StringComparison.Ordinal);
        Assert.Contains("-p:FileVersion=${{ steps.version.outputs.file_version }}", workflow, StringComparison.Ordinal);
        Assert.Contains("-p:AssemblyVersion=${{ steps.version.outputs.assembly_version }}", workflow, StringComparison.Ordinal);
        Assert.Contains("ProductVersion incorrecta", workflow, StringComparison.Ordinal);
        Assert.Contains("FileVersion incorrecta", workflow, StringComparison.Ordinal);
        Assert.Contains("--icon src/PeluqueriaAdmin.App/Assets/kv-logo.ico", workflow, StringComparison.Ordinal);
        Assert.Contains("$notesFile = \"docs/NOTAS_VERSION_$version.md\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--notes-file $notesFile", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_", updater, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github_pat_", updater, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--sign", workflow, StringComparison.OrdinalIgnoreCase);
    }
}
