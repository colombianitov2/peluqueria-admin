using System.IO;
using System.Security.Cryptography;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase52UiAndBrandingTests
{
    [Fact]
    public void HomeMovementDetail_WrapsAndLetsEachRowGrowAutomatically()
    {
        string home = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "HomeView.xaml");
        string styles = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "App.xaml");

        Assert.DoesNotContain("RowHeight=", home, StringComparison.Ordinal);
        Assert.Contains(
            "ElementStyle=\"{StaticResource WrappingCellText}\" Header=\"Detalle\"",
            home,
            StringComparison.Ordinal);
        Assert.Contains("x:Key=\"WrappingCellText\"", styles, StringComparison.Ordinal);
        Assert.Contains("TextWrapping\" Value=\"Wrap\"", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void KvLogo_IsExactSourceAndIcoContainsEveryRequiredResolution()
    {
        string assets = Path.Combine(
            RepositoryFiles.Root, "src", "PeluqueriaAdmin.App", "Assets");
        string source = Path.Combine(assets, "kv-logo-original.jpeg");
        string icon = Path.Combine(assets, "kv-logo.ico");

        Assert.Equal(
            "F276EAF78A03D378CDD4A3E9D0BF008165A139AB24FF606DAF94FFB4773CBC33",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
        byte[] bytes = File.ReadAllBytes(icon);
        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
        ushort count = BitConverter.ToUInt16(bytes, 4);
        Assert.Equal(7, count);
        int[] sizes = Enumerable.Range(0, count)
            .Select(index =>
            {
                int width = bytes[6 + index * 16];
                return width == 0 ? 256 : width;
            })
            .Order()
            .ToArray();
        Assert.Equal([16, 24, 32, 48, 64, 128, 256], sizes);

        string project = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "PeluqueriaAdmin.App.csproj");
        string window = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "MainWindow.xaml");
        Assert.Contains("<ApplicationIcon>Assets\\kv-logo.ico</ApplicationIcon>", project);
        Assert.Contains("<Resource Include=\"Assets\\kv-logo.ico\" />", project);
        Assert.Contains("Icon=\"Assets/kv-logo.ico\"", window);
    }

    [Fact]
    public void DailyRateLanguageAndRefreshContract_AreVisibleWithoutWeeklyProration()
    {
        string localUse = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "LocalUseView.xaml");
        string settings = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "Views", "SettingsView.xaml");
        string settingsViewModel = RepositoryFiles.Read(
            "src", "PeluqueriaAdmin.App", "ViewModels", "SettingsViewModel.cs");

        Assert.Contains("Tarifa diaria vigente", localUse, StringComparison.Ordinal);
        Assert.Contains("Próximo cobro", localUse, StringComparison.Ordinal);
        Assert.Contains("domingo no consume saldo", localUse, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Tarifa diaria general por uso del local (USD)",
            settings,
            StringComparison.Ordinal);
        Assert.DoesNotContain("periodos completos de siete días", localUse, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("administrationService.NotifyDataChanged();", settingsViewModel);
    }
}
