namespace Sentinel.Approvals;

using Sentinel.Conversations;
using Sentinel.Dispatch;

public sealed class ApprovalLedger(IApprovalStore store, IAuditLog auditLog, ConversationJournal journal, TimeProvider timeProvider)
{
    public DateTimeOffset Now => timeProvider.GetUtcNow();

    public Task RecordAsync(ConversationKey key, AuditAction action, Guid? approvalId = null, string? detail = null) =>
        auditLog.RecordAsync(new AuditEntry(Now, key.OperatorId, action, key.TargetId, approvalId, detail));

    public async Task<PendingApproval> OpenAsync(PendingApproval approval, string entryText)
    {
        await store.AddAsync(approval);
        await RecordAsync(approval.Conversation, AuditAction.ApprovalRequested, approval.Id, approval.Tool);
        await journal.AppendAsync(approval.Conversation, TranscriptRole.Agent, TranscriptEntryKind.ApprovalRequested, entryText, approval.Id);
        return approval;
    }
}
