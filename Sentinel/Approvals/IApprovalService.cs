namespace Sentinel.Approvals;

public interface IApprovalService
{
    Task<IReadOnlyList<PendingApproval>> GetPendingAsync(Guid operatorId);

    Task<bool> DecideAsync(Guid operatorId, Guid approvalId, bool approved, string? reason, CancellationToken cancellationToken);
}
