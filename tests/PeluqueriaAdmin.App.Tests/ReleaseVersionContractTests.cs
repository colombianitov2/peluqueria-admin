namespace PeluqueriaAdmin.App.Tests;

public sealed class ReleaseVersionContractTests
{
    [Fact]
    public void Phase51_VersionMetadataAndDocumentationAreConsistent()
    {
        string project = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "PeluqueriaAdmin.App.csproj");
        string manual = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "ManualView.xaml");
        string externalManual = RepositoryFiles.Read("docs", "MANUAL_USUARIO.md");
        string releaseNotes = RepositoryFiles.Read("docs", "NOTAS_VERSION_0.2.0-alpha.1.md");

        Assert.Contains("<Version>0.2.0-alpha.1</Version>", project, StringComparison.Ordinal);
        Assert.Contains("<AssemblyVersion>0.2.0.0</AssemblyVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<FileVersion>0.2.0.0</FileVersion>", project, StringComparison.Ordinal);
        Assert.Contains("Versión documentada: 0.2.0-alpha.1", manual, StringComparison.Ordinal);
        Assert.Contains("Versión documentada: **0.2.0-alpha.1**", externalManual, StringComparison.Ordinal);
        Assert.Contains("Versión preliminar pública", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("no está firmado digitalmente", releaseNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase51_ReleaseAndUpdaterUseThePublicRepositoryWithoutEmbeddedCredentials()
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
        Assert.DoesNotContain("ghp_", updater, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github_pat_", updater, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--sign", workflow, StringComparison.OrdinalIgnoreCase);
    }
}
