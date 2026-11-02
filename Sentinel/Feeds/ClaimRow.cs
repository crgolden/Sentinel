namespace Sentinel.Feeds;

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class ClaimRow
{
    public required string SessionId { get; init; }

    public required string Repo { get; init; }

    public required DateTimeOffset ClaimedAt { get; init; }

    public DateTimeOffset? ReleasedAt { get; init; }

    [AllowNull]
    public IReadOnlyList<string> Files { get => field; init => field = value ?? []; } = [];
}
