namespace Sentinel.Conversations;

public sealed record RunRecord(
    Guid RunId,
    RunState State,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string? TaskId,
    byte[]? ContinuationToken);
