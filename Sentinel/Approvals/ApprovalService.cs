namespace Sentinel.Approvals;

using Sentinel.Dispatch;

public sealed class ApprovalService(
    IApprovalStore store,
    IAuditLog auditLog,
    IDispatchQueue dispatchQueue,
    TimeProvider timeProvider,
    Telemetry telemetry) : IApprovalService
{
    public Task<IReadOnlyList<PendingApproval>> GetPendingAsync(Guid operatorId) => store.ListAsync(operatorId);

    public async Task<bool> DecideAsync(Guid operatorId, Guid approvalId, bool approved, string? reason, CancellationToken cancellationToken)
    {
        if (await store.TakeAsync(operatorId, approvalId) is not { } approval)
        {
            return false;
        }

        var normalizedReason = IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await auditLog.RecordAsync(new AuditEntry(
            timeProvider.GetUtcNow(),
            operatorId,
            approved ? AuditAction.ApprovalGranted : AuditAction.ApprovalRejected,
            approval.Conversation.TargetId,
            approval.Id,
            normalizedReason));
        telemetry.ApprovalDecided(approved);
        await dispatchQueue.EnqueueAsync(DispatchCommand.Decision(approval, approved, normalizedReason), cancellationToken);
        return true;
    }
}
