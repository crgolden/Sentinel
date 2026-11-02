namespace Sentinel.Tests.Unit.Dispatch;

using Microsoft.Extensions.Time.Testing;
using Moq;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ConversationJournalTests
{
    private readonly FakeTimeProvider _timeProvider = new(Generated.NewUtcTimestamp());
    private readonly Mock<IConversationStore> _store = Journals.AcceptingStore();
    private readonly ConversationEventHub _hub = new();
    private readonly ConversationKey _conversation = new(Generated.NewUserId(), Generated.LowercaseToken(8));
    private readonly ConversationJournal _journal;

    public ConversationJournalTests() => _journal = Journals.Create(_store, _hub, _timeProvider);

    [Fact]
    public async Task AppendAsync_StoresAndPublishesATimestampedEntry()
    {
        // Arrange
        List<ConversationEvent> received = [];
        using var subscription = _hub.Subscribe(_conversation, received.Add);
        var text = Generated.NewMessageText();

        // Act
        var entry = await _journal.AppendAsync(_conversation, TranscriptRole.Agent, TranscriptEntryKind.Message, text);

        // Assert
        Assert.Equal((_timeProvider.GetUtcNow(), text, _conversation.TargetId), (entry.At, entry.Text, entry.ListenerId));
        _store.Verify(store => store.AppendAsync(_conversation, entry), Times.Once);
        Assert.Equal(new ConversationEvent(_conversation, ConversationEventKind.EntryAppended, entry, null, null), Assert.Single(received));
    }

    [Fact]
    public async Task AppendAsync_OrchestratorConversation_HasNoListenerId()
    {
        // Arrange
        var conversation = ConversationKey.Orchestrator(Generated.NewUserId());

        // Act
        var entry = await _journal.AppendAsync(conversation, TranscriptRole.Operator, TranscriptEntryKind.Message, Generated.NewMessageText());

        // Assert
        Assert.Null(entry.ListenerId);
    }

    [Fact]
    public async Task StartRunAsync_SavesARunningRunAndPublishesTheState()
    {
        // Arrange
        List<ConversationEvent> received = [];
        using var subscription = _hub.Subscribe(_conversation, received.Add);

        // Act
        var run = await _journal.StartRunAsync(_conversation);

        // Assert
        Assert.Equal(RunState.Running, run.State);
        _store.Verify(store => store.SaveRunAsync(_conversation, run), Times.Once);
        Assert.Equal(RunState.Running, Assert.Single(received).State);
    }

    [Fact]
    public async Task SaveRunAsync_StampsTheUpdateTime()
    {
        // Arrange
        var run = await _journal.StartRunAsync(_conversation);
        _timeProvider.Advance(TimeSpan.FromSeconds(Generated.NewCount()));

        // Act
        var saved = await _journal.SaveRunAsync(_conversation, run with { State = RunState.Completed });

        // Assert
        Assert.Equal(_timeProvider.GetUtcNow(), saved.UpdatedAt);
    }

    [Fact]
    public void PublishDelta_PublishesTheText()
    {
        // Arrange
        List<ConversationEvent> received = [];
        using var subscription = _hub.Subscribe(_conversation, received.Add);
        var delta = Generated.NewMessageText();

        // Act
        _journal.PublishDelta(_conversation, delta);

        // Assert
        Assert.Equal(delta, Assert.Single(received).Delta);
    }
}
