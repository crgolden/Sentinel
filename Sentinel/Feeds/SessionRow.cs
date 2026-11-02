namespace Sentinel.Feeds;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SessionRow(string SessionId, string Machine, string Goal, Uri? Page, DateTimeOffset LastSeen);
