namespace Sentinel.Tests.Unit.TestSupport;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Sentinel.Conversations;
using Sentinel.Dispatch;

internal static class Journals
{
    internal static TranscriptTextOptions Text() => new(
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText());

    internal static ConversationJournal Create(Mock<IConversationStore> store, ConversationEventHub hub, FakeTimeProvider timeProvider) =>
        new(store.Object, hub, timeProvider, Options.Create(Text()));

    internal static Mock<IConversationStore> AcceptingStore()
    {
        var store = new Mock<IConversationStore>(MockBehavior.Strict);
        store.Setup(s => s.AppendAsync(It.IsAny<ConversationKey>(), It.IsAny<TranscriptEntry>())).Returns(Task.CompletedTask);
        store.Setup(s => s.SaveRunAsync(It.IsAny<ConversationKey>(), It.IsAny<RunRecord>())).Returns(Task.CompletedTask);
        return store;
    }
}
