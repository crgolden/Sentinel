namespace Sentinel.Approvals;

public sealed record AuditEntry(
    DateTimeOffset At,
    Guid OperatorId,
    AuditAction Action,
    string TargetId,
    Guid? ApprovalId,
    string? Detail);
