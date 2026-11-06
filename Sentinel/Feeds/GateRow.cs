namespace Sentinel.Feeds;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GateRow(string SessionId, string Machine, string Repo, GateState State, string? Verdict, DateTimeOffset At);
