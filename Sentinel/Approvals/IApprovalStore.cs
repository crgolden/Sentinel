namespace Sentinel.Approvals;

public interface IApprovalStore
{
    Task AddAsync(PendingApproval approval);

    Task<PendingApproval?> TakeAsync(Guid operatorId, Guid approvalId);

    Task<IReadOnlyList<PendingApproval>> ListAsync(Guid operatorId);
}
