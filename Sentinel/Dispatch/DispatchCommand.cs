namespace Sentinel.Dispatch;

using Sentinel.Approvals;
using Sentinel.Conversations;

public sealed record DispatchCommand(
    ConversationKey Conversation,
    DispatchCommandKind Kind,
    string? Text,
    ConversationKey? Origin,
    PendingApproval? Approval,
    bool Approved,
    string? Reason)
{
    public static DispatchCommand Message(ConversationKey conversation, string text, ConversationKey? origin = null) =>
        new(conversation, DispatchCommandKind.Message, text, origin, null, false, null);

    public static DispatchCommand Decision(PendingApproval approval, bool approved, string? reason) =>
        new(approval.Conversation, DispatchCommandKind.ApprovalDecision, null, approval.Origin, approval, approved, reason);

    public static DispatchCommand Resume(ConversationKey conversation) =>
        new(conversation, DispatchCommandKind.Resume, null, null, null, false, null);
}
