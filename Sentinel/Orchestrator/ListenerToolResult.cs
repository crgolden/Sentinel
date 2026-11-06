namespace Sentinel.Orchestrator;

using JetBrains.Annotations;
using Sentinel.Conversations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ListenerToolResult(string ListenerId, RunState Status, string? Text, Guid? ApprovalId);
