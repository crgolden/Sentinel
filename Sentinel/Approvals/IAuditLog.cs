namespace Sentinel.Approvals;

public interface IAuditLog : IAuditReader
{
    Task RecordAsync(AuditEntry entry);
}
