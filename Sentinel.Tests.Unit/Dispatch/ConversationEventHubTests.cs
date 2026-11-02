namespace Sentinel.Tests.Unit.Dispatch;

using Sentinel.Conversations;
using Sentinel.Dispatch;

[Trait("Category", "Unit")]
public sealed class ConversationEventHubTests
{
    private readonly ConversationEventHub _hub = new();
    private readonly ConversationKey _conversation = new(Generated.NewUserId(), Generated.LowercaseToken(8));

    [Fact]
    public void Publish_SubscribedConversation_DeliversTheEvent()
    {
        // Arrange
        List<ConversationEvent> received = [];
        using var subscription = _hub.Subscribe(_conversation, received.Add);
        var published = Delta(_conversation);

        // Act
        _hub.Publish(published);

        // Assert
        Assert.Equal([published], received);
    }

    [Fact]
    public void Publish_OtherConversation_IsNotDelivered()
    {
        // Arrange
        List<ConversationEvent> received = [];
        using var subscription = _hub.Subscribe(_conversation, received.Add);

        // Act
        _hub.Publish(Delta(_conversation with { TargetId = Generated.LowercaseToken(9) }));

        // Assert
        Assert.Empty(received);
    }

    [Fact]
    public void Publish_AfterDispose_IsNotDelivered()
    {
        // Arrange
        List<ConversationEvent> received = [];
        _hub.Subscribe(_conversation, received.Add).Dispose();

        // Act
        _hub.Publish(Delta(_conversation));

        // Assert
        Assert.Empty(received);
    }

    [Fact]
    public void SubscribeAll_DeliversEveryConversationOfTheOperatorOnly()
    {
        // Arrange
        List<ConversationEvent> received = [];
        using var subscription = _hub.SubscribeAll(_conversation.OperatorId, received.Add);
        var sameOperator = Delta(_conversation with { TargetId = Generated.LowercaseToken(9) });
        _hub.Publish(Delta(new ConversationKey(Generated.NewUserId(), _conversation.TargetId)));

        // Act
        _hub.Publish(sameOperator);

        // Assert
        Assert.Equal([sameOperator], received);
    }

    private static ConversationEvent Delta(ConversationKey conversation) =>
        new(conversation, ConversationEventKind.Delta, null, Generated.NewMessageText(), null);
}
