namespace Sentinel.Approvals;

using Sentinel.Conversations;

public sealed record PendingApproval(
    Guid Id,
    ApprovalKind Kind,
    ConversationKey Conversation,
    ConversationKey? Origin,
    string Tool,
    string ArgumentsJson,
    ApprovalRisk Risk,
    string Summary,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ExpiresAt,
    string? ListenerApprovalId,
    OrchestratorToolCall? ToolCall);
