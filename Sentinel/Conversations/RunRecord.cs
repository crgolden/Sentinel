namespace Sentinel.Conversations;

using System.Diagnostics.CodeAnalysis;

public sealed record RunRecord(
    Guid RunId,
    RunState State,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string? TaskId,
    [AllowNull] byte[] ContinuationToken)
{
    [AllowNull]
    public byte[] ContinuationToken { get => field; init => field = value ?? []; } = ContinuationToken ?? [];
}
