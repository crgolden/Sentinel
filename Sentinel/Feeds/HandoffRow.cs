namespace Sentinel.Feeds;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record HandoffRow(string Id, string From, string To, string Summary, string Status, string? Kind, DateTimeOffset At);
