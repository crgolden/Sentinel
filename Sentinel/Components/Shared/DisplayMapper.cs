namespace Sentinel.Components.Shared;

using System.Globalization;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Feeds;
using Sentinel.Listeners;

public static class DisplayMapper
{
    public const string Success = "bg-success";

    public const string Danger = "bg-danger";

    public const string Warning = "bg-warning text-dark";

    public const string Neutral = "bg-secondary";

    public const string Info = "bg-info text-dark";

    public static string BadgeClass(ListenerState state) => state switch
    {
        ListenerState.Online => Success,
        ListenerState.Offline => Danger,
        ListenerState.Misconfigured => Warning,
        _ => Neutral,
    };

    public static string BadgeClass(ListenerHealth health) => health switch
    {
        ListenerHealth.Healthy => Success,
        ListenerHealth.Degraded => Warning,
        _ => Danger,
    };

    public static string BadgeClass(RunState state) => state switch
    {
        RunState.Running => Info,
        RunState.AwaitingApproval => Warning,
        RunState.Completed => Success,
        RunState.Failed => Danger,
        _ => Neutral,
    };

    public static string BadgeClass(ApprovalRisk risk) => risk == ApprovalRisk.High ? Danger : Warning;

    public static string BorderClass(ListenerSnapshot snapshot) =>
        snapshot.Definition.Kind == ListenerKind.Master ? "border-primary" : snapshot.State switch
        {
            ListenerState.Online => "border-success",
            ListenerState.Offline => "border-danger",
            ListenerState.Misconfigured => "border-warning",
            _ => "border-secondary",
        };

    public static string EntryClass(TranscriptEntry entry) => entry.Kind switch
    {
        TranscriptEntryKind.Error => "list-group-item-danger",
        TranscriptEntryKind.ApprovalRequested => "list-group-item-warning",
        TranscriptEntryKind.ApprovalDecided => "list-group-item-info",
        TranscriptEntryKind.Status => "list-group-item-light",
        _ => entry.Role == TranscriptRole.Operator ? "list-group-item-primary" : "list-group-item",
    };

    public static string Label(ListenerState state) => state switch
    {
        ListenerState.Online => "Online",
        ListenerState.Offline => "Offline",
        ListenerState.Misconfigured => "Misconfigured",
        _ => "Checking",
    };

    public static string Label(ListenerProblem problem) => problem switch
    {
        ListenerProblem.Unreachable => "The agent card could not be fetched.",
        ListenerProblem.NoInterfaces => "The agent card lists no interfaces.",
        ListenerProblem.InsecureInterface => "The agent card lists an interface that is not https.",
        ListenerProblem.ForeignOrigin => "The agent card points outside the configured address.",
        _ => "No problem reported.",
    };

    public static string Label(RunState state) => state switch
    {
        RunState.Running => "Running",
        RunState.AwaitingApproval => "Awaiting approval",
        RunState.Completed => "Completed",
        RunState.Failed => "Failed",
        RunState.Interrupted => "Interrupted",
        _ => "Canceled",
    };

    public static string Label(TranscriptRole role) => role switch
    {
        TranscriptRole.Operator => "You",
        TranscriptRole.Agent => "Agent",
        _ => "Sentinel",
    };

    public static string Timestamp(DateTimeOffset? at) =>
        at is { } value ? value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) : "Never";

    public static string ElementId(string prefix, Guid id) => $"{prefix}-{id:N}";

    public static string ElementId(string prefix, string id) => $"{prefix}-{id}";
}
