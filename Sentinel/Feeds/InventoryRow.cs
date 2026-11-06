namespace Sentinel.Feeds;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record InventoryRow(string Machine, string SessionId, string State, string Goal, DateTimeOffset At);
