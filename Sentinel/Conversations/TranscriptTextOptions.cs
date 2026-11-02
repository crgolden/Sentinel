namespace Sentinel.Conversations;

public sealed record TranscriptTextOptions(
    string ListenerUnavailable,
    string RunFailed,
    string RunInterrupted,
    string RunCanceled,
    string ApprovalPending,
    string ApprovalGranted,
    string ApprovalRejected,
    string ListenerRepliedAfterApproval);
