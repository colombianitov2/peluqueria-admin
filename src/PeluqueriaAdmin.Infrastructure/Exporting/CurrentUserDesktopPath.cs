using PeluqueriaAdmin.Application.Exporting;

namespace PeluqueriaAdmin.Infrastructure.Exporting;

public sealed class CurrentUserDesktopPath : IUserDesktopPath
{
    public string GetDesktopPath() => Environment.GetFolderPath(
        Environment.SpecialFolder.DesktopDirectory,
        Environment.SpecialFolderOption.DoNotVerify);
}
