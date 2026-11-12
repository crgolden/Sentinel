namespace Sentinel.Dispatch;

using Microsoft.Extensions.Options;
using Sentinel.Conversations;

public sealed class DispatchWorker(
    DispatchQueue queue,
    IListenerRunner listenerRunner,
    IOrchestratorRunner orchestratorRunner,
    IConversationStore store,
    ConversationJournal journal,
    IOptions<DispatchOptions> options,
    Telemetry telemetry) : BackgroundService
{
    private readonly SemaphoreSlim _concurrency = new(options.Value.MaxConcurrentRuns, options.Value.MaxConcurrentRuns);
    private readonly List<Task> _inFlight = [];
    private readonly Lock _inFlightLock = new();

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        Task[] remaining;
        lock (_inFlightLock)
        {
            remaining = [.. _inFlight];
        }

        await Task.WhenAll(remaining).WaitAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _concurrency.Dispose();
        base.Dispose();
    }

    internal async Task RecoverAsync(CancellationToken cancellationToken)
    {
        foreach (var key in await store.GetActiveRunsAsync())
        {
            if (!key.IsOrchestrator && await store.GetRunAsync(key) is { ContinuationToken.Length: > 0 })
            {
                await queue.EnqueueAsync(DispatchCommand.Resume(key), cancellationToken);
            }
            else if (await store.GetRunAsync(key) is { } run)
            {
                await journal.AppendAsync(key, TranscriptRole.System, TranscriptEntryKind.Status, journal.Text.RunInterrupted);
                await journal.SaveRunAsync(key, run with { State = RunState.Interrupted });
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);
        await foreach (var command in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await _concurrency.WaitAsync(stoppingToken);
            var task = ProcessAsync(command, stoppingToken);
            lock (_inFlightLock)
            {
                _inFlight.Add(task);
            }

            _ = task.ContinueWith(
                completed =>
                {
                    lock (_inFlightLock)
                    {
                        _inFlight.Remove(completed);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task ProcessAsync(DispatchCommand command, CancellationToken stoppingToken)
    {
        await Task.Yield();
        var source = queue.Track(command.Conversation, stoppingToken);
        try
        {
            if (command.Conversation.IsOrchestrator)
            {
                await orchestratorRunner.RunAsync(command, source.Token);
            }
            else
            {
                await listenerRunner.RunAsync(command, source.Token);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            telemetry.DispatchFailed(command.Conversation.TargetId, ex);
            await journal.AppendAsync(command.Conversation, TranscriptRole.System, TranscriptEntryKind.Error, journal.Text.RunFailed);
        }
        finally
        {
            queue.Untrack(command.Conversation, source);
            _concurrency.Release();
        }
    }
}
