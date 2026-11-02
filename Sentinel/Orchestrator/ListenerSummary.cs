namespace Sentinel.Orchestrator;

using JetBrains.Annotations;
using Sentinel.Listeners;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ListenerSummary(string Id, string DisplayName, ListenerKind Kind, ListenerState State, string? Description, IReadOnlyList<string> Skills);
