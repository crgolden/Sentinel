namespace Sentinel.Tests.Unit.Components;

using AngleSharp.Dom;
using Bunit;
using Moq;
using Sentinel.Components.Shared;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ChatPanelTests : IDisposable
{
    private readonly ComponentHarness _harness = new();
    private readonly ConversationKey _conversation = new(Generated.NewUserId(), Generated.LowercaseToken(8));

    [Fact]
    public void Render_ExistingTranscript_ShowsEveryEntryAndTheRunState()
    {
        // Arrange
        var operatorEntry = Transcripts.Entry(TranscriptRole.Operator);
        var agentEntry = Transcripts.Entry(TranscriptRole.Agent);
        _harness.Reader.Setup(reader => reader.GetTranscriptAsync(_conversation)).ReturnsAsync([operatorEntry, agentEntry]);
        _harness.Reader.Setup(reader => reader.GetRunAsync(_conversation)).ReturnsAsync(Transcripts.Run(RunState.Completed));

        // Act
        var cut = Render();

        // Assert
        Assert.Contains(operatorEntry.Text, cut.Find($"#{DisplayMapper.ElementId(ElementIds.TranscriptEntry, operatorEntry.Id)}").TextContent, StringComparison.Ordinal);
        Assert.Contains(agentEntry.Text, cut.Find($"#{DisplayMapper.ElementId(ElementIds.TranscriptEntry, agentEntry.Id)}").TextContent, StringComparison.Ordinal);
        Assert.Equal(DisplayMapper.Label(RunState.Completed), cut.Find("#chat-run-state").TextContent);
    }

    [Fact]
    public void Render_EmptyConversation_SaysSo()
    {
        // Arrange
        _harness.EmptyConversation(_conversation);

        // Act
        var cut = Render();

        // Assert
        Assert.NotNull(cut.Find("#chat-empty"));
    }

    [Fact]
    public void Submit_Draft_EnqueuesTheTrimmedMessageAndClearsTheInput()
    {
        // Arrange
        var text = Generated.NewMessageText();
        _harness.EmptyConversation(_conversation);
        _harness.Queue.Setup(queue => queue.EnqueueAsync(DispatchCommand.Message(_conversation, text, null), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        var cut = Render();
        cut.Find("#chat-input").Input(Generated.Padded(text));

        // Act
        cut.Find("#chat-form").Submit();

        // Assert
        _harness.Queue.Verify(queue => queue.EnqueueAsync(DispatchCommand.Message(_conversation, text, null), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(string.IsNullOrWhiteSpace(cut.Find("#chat-input").GetAttribute(AttributeNames.Value)));
    }

    [Fact]
    public void Events_DeltaThenAgentEntry_StreamsAndThenReplacesTheStreamWithTheEntry()
    {
        // Arrange
        _harness.EmptyConversation(_conversation);
        var cut = Render();
        var delta = Generated.NewMessageText();
        _harness.Hub.Publish(new ConversationEvent(_conversation, ConversationEventKind.Delta, null, delta, null));
        var streamed = cut.Find("#chat-streaming").TextContent;
        var entry = Transcripts.Entry(TranscriptRole.Agent);

        // Act
        _harness.Hub.Publish(new ConversationEvent(_conversation, ConversationEventKind.EntryAppended, entry, null, null));

        // Assert
        Assert.Contains(delta, streamed, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("#chat-streaming"));
        Assert.NotNull(cut.Find($"#{DisplayMapper.ElementId(ElementIds.TranscriptEntry, entry.Id)}"));
    }

    [Fact]
    public void Events_RunningState_DisablesSendingAndOffersCancel()
    {
        // Arrange
        _harness.EmptyConversation(_conversation);
        var cut = Render();

        // Act
        _harness.Hub.Publish(new ConversationEvent(_conversation, ConversationEventKind.RunStateChanged, null, null, RunState.Running));

        // Assert
        Assert.True(cut.Find("#chat-send").HasAttribute(AttributeNames.Disabled));
        Assert.NotNull(cut.Find("#chat-cancel"));
    }

    [Fact]
    public void Cancel_RunningTurn_CancelsTheConversation()
    {
        // Arrange
        _harness.Reader.Setup(reader => reader.GetTranscriptAsync(_conversation)).ReturnsAsync([]);
        _harness.Reader.Setup(reader => reader.GetRunAsync(_conversation)).ReturnsAsync(Transcripts.Run(RunState.Running));
        _harness.Queue.Setup(queue => queue.Cancel(_conversation)).Returns(true);
        var cut = Render();

        // Act
        cut.Find("#chat-cancel").Click();

        // Assert
        _harness.Queue.Verify(queue => queue.Cancel(_conversation), Times.Once);
    }

    [Fact]
    public void Render_Disabled_DisablesTheInput()
    {
        // Arrange
        _harness.EmptyConversation(_conversation);

        // Act
        var cut = _harness.Context.Render<ChatPanel>(parameters => parameters
            .Add(p => p.Conversation, _conversation)
            .Add(p => p.Title, Generated.NewDisplayName())
            .Add(p => p.Enabled, false));

        // Assert
        Assert.True(cut.Find("#chat-input").HasAttribute(AttributeNames.Disabled));
    }

    public void Dispose() => _harness.Dispose();

    private IRenderedComponent<ChatPanel> Render() =>
        _harness.Context.Render<ChatPanel>(parameters => parameters
            .Add(p => p.Conversation, _conversation)
            .Add(p => p.Title, Generated.NewDisplayName()));
}
