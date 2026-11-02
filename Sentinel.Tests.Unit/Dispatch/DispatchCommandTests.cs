namespace Sentinel.Tests.Unit.Dispatch;

using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Dispatch;

[Trait("Category", "Unit")]
public sealed class DispatchCommandTests
{
    [Fact]
    public void Message_CarriesTheTextAndOrigin()
    {
        // Arrange
        var conversation = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));
        var origin = ConversationKey.Orchestrator(conversation.OperatorId);
        var text = Generated.NewMessageText();

        // Act
        var command = DispatchCommand.Message(conversation, text, origin);

        // Assert
        Assert.Equal(new DispatchCommand(conversation, DispatchCommandKind.Message, text, origin, null, false, null), command);
    }

    [Fact]
    public void Decision_TargetsTheApprovalsConversation()
    {
        // Arrange
        var approval = new PendingApproval(
            Guid.NewGuid(),
            ApprovalKind.ListenerInput,
            new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8)),
            null,
            Generated.LowercaseToken(8),
            Generated.NewMessageText(),
            ApprovalRisk.High,
            Generated.NewMessageText(),
            Generated.NewUtcTimestamp(),
            null,
            null,
            null);
        var reason = Generated.NewMessageText();

        // Act
        var command = DispatchCommand.Decision(approval, true, reason);

        // Assert
        Assert.Equal(new DispatchCommand(approval.Conversation, DispatchCommandKind.ApprovalDecision, null, null, approval, true, reason), command);
    }

    [Fact]
    public void Resume_HasNoPayload()
    {
        // Arrange
        var conversation = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));

        // Act
        var command = DispatchCommand.Resume(conversation);

        // Assert
        Assert.Equal(new DispatchCommand(conversation, DispatchCommandKind.Resume, null, null, null, false, null), command);
    }
}
