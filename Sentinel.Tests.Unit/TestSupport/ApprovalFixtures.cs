namespace Sentinel.Tests.Unit.TestSupport;

using System.Text.Json;
using Sentinel.Approvals;
using Sentinel.Conversations;

internal static class ApprovalFixtures
{
    internal static PendingApproval Pending(ApprovalKind kind = ApprovalKind.ListenerInput, ConversationKey? conversation = null)
    {
        var approvalId = Guid.NewGuid();
        return new PendingApproval(
            approvalId,
            kind,
            conversation ?? new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8)),
            null,
            Generated.LowercaseToken(8),
            JsonSerializer.Serialize(new Dictionary<string, string> { [Generated.LowercaseToken(5)] = Generated.LowercaseToken(6) }),
            ApprovalRisk.High,
            Generated.NewMessageText(),
            Generated.NewUtcTimestamp(),
            Generated.NewUtcTimestamp(),
            Generated.LowercaseToken(12),
            null);
    }
}
