namespace Sentinel.Approvals;

public interface IAuditReader
{
    Task<IReadOnlyList<AuditEntry>> ReadLatestAsync(int count);
}
