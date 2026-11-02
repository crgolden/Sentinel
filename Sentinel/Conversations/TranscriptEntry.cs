namespace Sentinel.Conversations;

public sealed record TranscriptEntry(
    Guid Id,
    DateTimeOffset At,
    TranscriptRole Role,
    TranscriptEntryKind Kind,
    string Text,
    string? ListenerId,
    Guid? ApprovalId);
