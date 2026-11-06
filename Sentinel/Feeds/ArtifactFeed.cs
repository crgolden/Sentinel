namespace Sentinel.Feeds;

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class ArtifactFeed
{
    public required DateTimeOffset GeneratedAt { get; init; }

    [AllowNull]
    public IReadOnlyList<ArtifactItem> Items { get => field; init => field = value ?? []; } = [];
}
