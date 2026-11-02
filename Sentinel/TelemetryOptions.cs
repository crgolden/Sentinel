namespace Sentinel;

public sealed record TelemetryOptions(
    string ListenerRefreshFailureDescription,
    string FeedRefreshFailureDescription,
    string DispatchFailureDescription,
    string ApprovalDecisionDescription);
