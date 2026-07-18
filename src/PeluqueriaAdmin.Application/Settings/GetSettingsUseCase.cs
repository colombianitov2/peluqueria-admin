namespace PeluqueriaAdmin.Application.Settings;

public sealed class GetSettingsUseCase(ISettingsRepository repository)
{
    public async Task<SettingsDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var settings = await repository.GetAsync(cancellationToken);
        return SettingsMapper.ToDto(settings);
    }
}
