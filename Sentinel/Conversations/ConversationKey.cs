namespace Sentinel.Conversations;

using Sentinel.Listeners;

public sealed record ConversationKey(Guid OperatorId, string TargetId)
{
    public bool IsOrchestrator => string.Equals(TargetId, ListenerCatalog.OrchestratorTargetId, StringComparison.Ordinal);

    public static ConversationKey Orchestrator(Guid operatorId) => new(operatorId, ListenerCatalog.OrchestratorTargetId);

    public override string ToString() => $"{OperatorId:N}:{TargetId}";
}
