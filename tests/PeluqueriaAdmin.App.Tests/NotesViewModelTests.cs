using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Application.Notes;
using PeluqueriaAdmin.Domain.Notes;

namespace PeluqueriaAdmin.App.Tests;

public sealed class NotesViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DebouncePersistsAndReopeningRestoresExactContent()
    {
        var repository = new MemoryNoteRepository();
        var first = new NotesViewModel(repository, new FixedTimeProvider(Now));
        await first.LoadAsync(TestContext.Current.CancellationToken);

        first.Content = "Primera línea\r\nSegunda línea = conservada";
        await Task.Delay(650, TestContext.Current.CancellationToken);

        Assert.Equal(first.Content, repository.Stored?.Content);
        var reopened = new NotesViewModel(repository, new FixedTimeProvider(Now.AddMinutes(1)));
        await reopened.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Primera línea\r\nSegunda línea = conservada", reopened.Content);
    }

    [Fact]
    public async Task ForcedFlushPersistsWithoutWaitingForDebounce()
    {
        var repository = new MemoryNoteRepository();
        var viewModel = new NotesViewModel(repository, new FixedTimeProvider(Now));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.Content = "Contenido al perder foco o cerrar";

        await viewModel.FlushPendingAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Contenido al perder foco o cerrar", repository.Stored?.Content);
        Assert.Equal(1, repository.SaveCount);
    }

    private sealed class MemoryNoteRepository : INoteRepository
    {
        public AppNote? Stored { get; private set; }
        public int SaveCount { get; private set; }

        public Task<AppNote?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stored);

        public Task SaveAsync(AppNote note, CancellationToken cancellationToken = default)
        {
            Stored = note;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
