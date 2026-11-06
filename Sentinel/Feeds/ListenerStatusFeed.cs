namespace Sentinel.Feeds;

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class ListenerStatusFeed
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required ListenerHealth Health { get; init; }

    public string? Summary { get; init; }

    [AllowNull]
    public IReadOnlyList<StatusCheck> Checks { get => field; init => field = value ?? []; } = [];
}
