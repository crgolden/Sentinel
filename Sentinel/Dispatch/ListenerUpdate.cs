namespace Sentinel.Dispatch;

using A2A;
using Sentinel.Approvals;

public sealed record ListenerUpdate(TaskState? State, string? TaskId, ApprovalRequestPart? Approval)
{
    public static readonly ListenerUpdate None = new(null, null, null);
}
