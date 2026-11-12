namespace Sentinel.Tests.Unit.Dispatch;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class DispatchWorkerTests : IDisposable
{
    private readonly TelemetryHarness _telemetry = new();
    private readonly DispatchQueue _queue = new(Options.Create(new DispatchOptions(Generated.NewCount(), 1, Generated.NewCount())));
    private readonly Mock<IListenerRunner> _listenerRunner = new(MockBehavior.Strict);
    private readonly Mock<IOrchestratorRunner> _orchestratorRunner = new(MockBehavior.Strict);
    private readonly Mock<IConversationStore> _store = Journals.AcceptingStore();
    private readonly DispatchWorker _worker;

    public DispatchWorkerTests() =>
        _worker = new DispatchWorker(
            _queue,
            _listenerRunner.Object,
            _orchestratorRunner.Object,
            _store.Object,
            Journals.Create(_store, new ConversationEventHub(), new FakeTimeProvider()),
            Options.Create(new DispatchOptions(Generated.NewCount(), 1, Generated.NewCount())),
            _telemetry.Telemetry);

    [Fact]
    public async Task ExecuteAsync_ListenerCommand_RunsTheListenerRunner()
    {
        // Arrange
        var command = DispatchCommand.Message(new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8)), Generated.NewMessageText());
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.Setup(store => store.GetActiveRunsAsync()).ReturnsAsync([]);
        _listenerRunner.Setup(runner => runner.RunAsync(command, It.IsAny<CancellationToken>()))
            .Callback(ran.SetResult)
            .ReturnsAsync(new ListenerTurnResult(RunState.Completed, null, null));
        await _worker.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await _queue.EnqueueAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await ran.Task.WaitAsync(TestContext.Current.CancellationToken);
        await _worker.StopAsync(TestContext.Current.CancellationToken);
        _listenerRunner.Verify(runner => runner.RunAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OrchestratorCommand_RunsTheOrchestratorRunner()
    {
        // Arrange
        var command = DispatchCommand.Message(ConversationKey.Orchestrator(Generated.NewUserId()), Generated.NewMessageText());
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.Setup(store => store.GetActiveRunsAsync()).ReturnsAsync([]);
        _orchestratorRunner.Setup(runner => runner.RunAsync(command, It.IsAny<CancellationToken>()))
            .Callback(ran.SetResult)
            .Returns(Task.CompletedTask);
        await _worker.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await _queue.EnqueueAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await ran.Task.WaitAsync(TestContext.Current.CancellationToken);
        await _worker.StopAsync(TestContext.Current.CancellationToken);
        _orchestratorRunner.Verify(runner => runner.RunAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RunnerThrows_RecordsAnErrorEntryAndCountsTheFailure()
    {
        // Arrange
        using var capture = _telemetry.Capture(Telemetry.Metrics.DispatchFailureCounterName);
        var conversation = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));
        var command = DispatchCommand.Message(conversation, Generated.NewMessageText());
        var appended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.Setup(store => store.GetActiveRunsAsync()).ReturnsAsync([]);
        _store.Setup(store => store.AppendAsync(conversation, It.Is<TranscriptEntry>(entry => entry.Kind == TranscriptEntryKind.Error)))
            .Callback(appended.SetResult)
            .Returns(Task.CompletedTask);
        _listenerRunner.Setup(runner => runner.RunAsync(command, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        await _worker.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await _queue.EnqueueAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await appended.Task.WaitAsync(TestContext.Current.CancellationToken);
        await _worker.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(conversation.TargetId, Assert.Single(capture.Measurements).Tags[Telemetry.Metrics.TargetTagName]);
    }

    [Fact]
    public async Task RecoverAsync_ListenerRunWithAContinuationToken_IsQueuedToResume()
    {
        // Arrange
        var conversation = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));
        var continuationToken = Guid.NewGuid().ToByteArray();
        _store.Setup(store => store.GetActiveRunsAsync()).ReturnsAsync([conversation]);
        _store.Setup(store => store.GetRunAsync(conversation)).ReturnsAsync(Run(continuationToken));

        // Act
        await _worker.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DispatchCommand.Resume(conversation), await _queue.Reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecoverAsync_RunWithoutAToken_IsMarkedInterrupted()
    {
        // Arrange
        var conversation = ConversationKey.Orchestrator(Generated.NewUserId());
        _store.Setup(store => store.GetActiveRunsAsync()).ReturnsAsync([conversation]);
        _store.Setup(store => store.GetRunAsync(conversation)).ReturnsAsync(Run([]));

        // Act
        await _worker.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        _store.Verify(store => store.SaveRunAsync(conversation, It.Is<RunRecord>(run => run.State == RunState.Interrupted)), Times.Once);
        _store.Verify(store => store.AppendAsync(conversation, It.Is<TranscriptEntry>(entry => entry.Kind == TranscriptEntryKind.Status)), Times.Once);
    }

    public void Dispose()
    {
        _worker.Dispose();
        _telemetry.Dispose();
    }

    private static RunRecord Run(byte[] continuationToken)
    {
        var runId = Guid.NewGuid();
        var startedAt = Generated.NewUtcTimestamp();
        var updatedAt = Generated.NewUtcTimestamp();
        var taskId = Generated.LowercaseToken(10);
        return new RunRecord(runId, RunState.Running, startedAt, updatedAt, taskId, continuationToken);
    }
}
