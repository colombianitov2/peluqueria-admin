using CommunityToolkit.Mvvm.ComponentModel;
using PeluqueriaAdmin.Application.Notes;
using PeluqueriaAdmin.Domain.Notes;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class NotesViewModel(INoteRepository repository, TimeProvider timeProvider) : ObservableObject
{
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private CancellationTokenSource? saveCancellation;
    private AppNote? note;
    private bool suppressChanges;

    [ObservableProperty] private string content = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        note = await repository.GetAsync(cancellationToken);
        suppressChanges = true;
        Content = note?.Content ?? string.Empty;
        suppressChanges = false;
        StatusMessage = string.Empty;
    }

    public async Task FlushPendingAsync(CancellationToken cancellationToken = default)
    {
        saveCancellation?.Cancel();
        await SaveAsync(cancellationToken);
    }

    partial void OnContentChanged(string value)
    {
        if (suppressChanges) return;
        saveCancellation?.Cancel();
        saveCancellation = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(saveCancellation.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            await SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
            if (note is null)
            {
                note = AppNote.Create(Content, utcNow);
            }
            else
            {
                note.Update(Content, utcNow);
            }
            await repository.SaveAsync(note, cancellationToken);
            StatusMessage = "Guardado automáticamente";
        }
        finally
        {
            saveLock.Release();
        }
    }
}
