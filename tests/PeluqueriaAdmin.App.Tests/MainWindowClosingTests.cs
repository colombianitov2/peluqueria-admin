using System.ComponentModel;
using PeluqueriaAdmin.App;

namespace PeluqueriaAdmin.App.Tests;

public sealed class MainWindowClosingTests
{
    [Fact]
    public async Task ImmediateFlush_SchedulesOneLaterCloseAndAuthorizesSecondClosing()
    {
        int flushCount = 0;
        int closeCount = 0;
        var scheduled = new List<Action>();
        var coordinator = new WindowCloseCoordinator(
            () =>
            {
                flushCount++;
                return Task.CompletedTask;
            },
            scheduled.Add,
            () => closeCount++,
            _ => Assert.Fail("No se esperaba un fallo."));
        var first = new CancelEventArgs();

        await coordinator.HandleClosingAsync(first);

        Assert.True(first.Cancel);
        Assert.Equal(1, flushCount);
        Assert.Single(scheduled);
        Assert.Equal(0, closeCount);

        scheduled.Single()();
        Assert.Equal(1, closeCount);

        var authorized = new CancelEventArgs();
        await coordinator.HandleClosingAsync(authorized);

        Assert.False(authorized.Cancel);
        Assert.Equal(1, flushCount);
        Assert.Single(scheduled);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task DeferredFlush_IgnoresRepeatedClosingUntilOneFlushCompletes()
    {
        int flushCount = 0;
        int closeCount = 0;
        var scheduled = new List<Action>();
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new WindowCloseCoordinator(
            () =>
            {
                flushCount++;
                return completion.Task;
            },
            scheduled.Add,
            () => closeCount++,
            _ => Assert.Fail("No se esperaba un fallo."));
        var first = new CancelEventArgs();
        var second = new CancelEventArgs();
        var third = new CancelEventArgs();

        Task firstClosing = coordinator.HandleClosingAsync(first);
        await coordinator.HandleClosingAsync(second);
        await coordinator.HandleClosingAsync(third);

        Assert.True(first.Cancel);
        Assert.True(second.Cancel);
        Assert.True(third.Cancel);
        Assert.Equal(1, flushCount);
        Assert.Empty(scheduled);

        completion.SetResult();
        await firstClosing;

        Assert.Single(scheduled);
        scheduled.Single()();
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task FlushFailure_IsReportedAndKeepsClosingCancelled()
    {
        int flushCount = 0;
        int failureCount = 0;
        int closeCount = 0;
        var scheduled = new List<Action>();
        var expected = new InvalidOperationException("Fallo controlado de prueba.");
        var coordinator = new WindowCloseCoordinator(
            () =>
            {
                flushCount++;
                return Task.FromException(expected);
            },
            scheduled.Add,
            () => closeCount++,
            exception =>
            {
                Assert.Same(expected, exception);
                failureCount++;
            });
        var closing = new CancelEventArgs();

        await coordinator.HandleClosingAsync(closing);

        Assert.True(closing.Cancel);
        Assert.Equal(1, flushCount);
        Assert.Equal(1, failureCount);
        Assert.Empty(scheduled);
        Assert.Equal(0, closeCount);
    }

    [Fact]
    public async Task FlushFailure_AllowsASafeRetry()
    {
        int flushCount = 0;
        int failureCount = 0;
        var scheduled = new List<Action>();
        var coordinator = new WindowCloseCoordinator(
            () =>
            {
                flushCount++;
                return flushCount == 1
                    ? Task.FromException(new InvalidOperationException("Primer intento."))
                    : Task.CompletedTask;
            },
            scheduled.Add,
            () => { },
            _ => failureCount++);

        var first = new CancelEventArgs();
        await coordinator.HandleClosingAsync(first);
        var retry = new CancelEventArgs();
        await coordinator.HandleClosingAsync(retry);

        Assert.True(first.Cancel);
        Assert.True(retry.Cancel);
        Assert.Equal(2, flushCount);
        Assert.Equal(1, failureCount);
        Assert.Single(scheduled);
    }

    [Fact]
    public async Task ScheduledCloseFailure_IsObservedWithoutRecursion()
    {
        int failureCount = 0;
        var scheduled = new List<Action>();
        var coordinator = new WindowCloseCoordinator(
            () => Task.CompletedTask,
            scheduled.Add,
            () => throw new InvalidOperationException("Cierre controlado de prueba."),
            _ => failureCount++);

        await coordinator.HandleClosingAsync(new CancelEventArgs());

        Action close = Assert.Single(scheduled);
        close();

        Assert.Equal(1, failureCount);
        Assert.Single(scheduled);
    }
}

public sealed class ApplicationExitCoordinatorTests
{
    [Fact]
    public void ImmediateBackup_CompletesAndRunsEveryExitStageOnce()
    {
        int prepareCount = 0;
        int backupCount = 0;
        int cleanupCount = 0;
        int baseExitCount = 0;
        var coordinator = new ApplicationExitCoordinator(
            () => prepareCount++,
            () =>
            {
                backupCount++;
                return Task.CompletedTask;
            },
            _ => Assert.Fail("No se esperaba un fallo."),
            () => cleanupCount++);

        coordinator.RunOnce(() => baseExitCount++);
        coordinator.RunOnce(() => baseExitCount++);

        Assert.Equal(1, prepareCount);
        Assert.Equal(1, backupCount);
        Assert.Equal(1, cleanupCount);
        Assert.Equal(1, baseExitCount);
    }

    [Fact]
    public void DeferredBackup_DoesNotCaptureTheBlockedCallerContext()
    {
        int callerThread = Environment.CurrentManagedThreadId;
        int backupThread = callerThread;
        var callerContext = new RecordingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(callerContext);
        try
        {
            var coordinator = new ApplicationExitCoordinator(
                () => { },
                async () =>
                {
                    backupThread = Environment.CurrentManagedThreadId;
                    Assert.Null(SynchronizationContext.Current);
                    await Task.Delay(25);
                    Assert.Null(SynchronizationContext.Current);
                },
                _ => Assert.Fail("No se esperaba un fallo."),
                () => { });

            coordinator.RunOnce(() => { });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.NotEqual(callerThread, backupThread);
        Assert.Equal(0, callerContext.PostCount);
    }

    [Fact]
    public void BackupFailure_IsReportedAndStillCleansUpAndExitsOnce()
    {
        int failureCount = 0;
        int cleanupCount = 0;
        int baseExitCount = 0;
        var expected = new InvalidOperationException("Fallo controlado de respaldo.");
        var coordinator = new ApplicationExitCoordinator(
            () => { },
            () => Task.FromException(expected),
            exception =>
            {
                Assert.Same(expected, exception);
                failureCount++;
            },
            () => cleanupCount++);

        coordinator.RunOnce(() => baseExitCount++);

        Assert.Equal(1, failureCount);
        Assert.Equal(1, cleanupCount);
        Assert.Equal(1, baseExitCount);
    }

    [Fact]
    public void DiagnosticFailure_DoesNotEscapeOrPreventBaseExit()
    {
        int baseExitCount = 0;
        var coordinator = new ApplicationExitCoordinator(
            () => { },
            () => Task.FromException(new InvalidOperationException("Respaldo.")),
            _ => throw new InvalidOperationException("Diagnóstico."),
            () => { });

        coordinator.RunOnce(() => baseExitCount++);

        Assert.Equal(1, baseExitCount);
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            PostCount++;
        }
    }
}
