namespace Sentinel.Dispatch;

using System.Text.Json;
using A2A;
using Sentinel.Approvals;

public static class ListenerUpdateClassifier
{
    private const string KindProperty = "kind";

    public static ListenerUpdate Classify(object? rawRepresentation) => rawRepresentation switch
    {
        Message message => new ListenerUpdate(null, message.TaskId, FindApproval(message.Parts)),
        AgentTask task => new ListenerUpdate(task.Status.State, task.Id, FindApproval(task.Status.Message?.Parts)),
        TaskStatusUpdateEvent statusUpdate => new ListenerUpdate(statusUpdate.Status.State, statusUpdate.TaskId, FindApproval(statusUpdate.Status.Message?.Parts)),
        TaskArtifactUpdateEvent artifactUpdate => new ListenerUpdate(null, artifactUpdate.TaskId, null),
        _ => ListenerUpdate.None,
    };

    public static ApprovalRequestPart? FindApproval(IEnumerable<Part>? parts) =>
        parts?
            .Select(part => part.Data)
            .OfType<JsonElement>()
            .Where(IsApprovalRequest)
            .Select(data => data.Deserialize<ApprovalRequestPart>(SentinelJson.Options))
            .OfType<ApprovalRequestPart>()
            .FirstOrDefault();

    public static Part ResponsePart(PendingApproval approval, bool approved, string? reason) =>
        Part.FromData(JsonSerializer.SerializeToElement(
            new ApprovalResponsePart(
                ApprovalContract.ResponseKind,
                approval.ListenerApprovalId ?? approval.Id.ToString("N"),
                approved,
                reason,
                approval.Conversation.OperatorId),
            SentinelJson.Options));

    private static bool IsApprovalRequest(JsonElement data) =>
        data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty(KindProperty, out var kind)
        && kind.ValueKind == JsonValueKind.String
        && string.Equals(kind.GetString(), ApprovalContract.RequestKind, StringComparison.Ordinal);
}
