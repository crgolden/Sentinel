namespace Sentinel.Feeds;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record StatusCheck(string Name, ListenerHealth Health, string? Detail, DateTimeOffset? CheckedAt);
