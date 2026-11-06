namespace Sentinel.Dispatch;

using Sentinel.Approvals;
using Sentinel.Conversations;

public sealed record ListenerTurnResult(RunState State, string? Text, PendingApproval? Approval);
