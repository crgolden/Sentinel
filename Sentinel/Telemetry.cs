namespace Sentinel;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

public sealed class Telemetry : IDisposable
{
    private readonly ActivitySource _activitySource = new(Metrics.SourceName);

    private readonly Counter<long> _listenerRefreshFailureCounter;
    private readonly Counter<long> _feedRefreshFailureCounter;
    private readonly Counter<long> _dispatchFailureCounter;
    private readonly Counter<long> _approvalDecisionCounter;

    public Telemetry(IMeterFactory meterFactory, IOptions<TelemetryOptions> telemetryOptions)
    {
        var meter = meterFactory.Create(Metrics.MeterName, typeof(Telemetry).Assembly.GetName().Version?.ToString());
        _listenerRefreshFailureCounter = meter.CreateCounter<long>(
            Metrics.ListenerRefreshFailureCounterName,
            description: telemetryOptions.Value.ListenerRefreshFailureDescription);
        _feedRefreshFailureCounter = meter.CreateCounter<long>(
            Metrics.FeedRefreshFailureCounterName,
            description: telemetryOptions.Value.FeedRefreshFailureDescription);
        _dispatchFailureCounter = meter.CreateCounter<long>(
            Metrics.DispatchFailureCounterName,
            description: telemetryOptions.Value.DispatchFailureDescription);
        _approvalDecisionCounter = meter.CreateCounter<long>(
            Metrics.ApprovalDecisionCounterName,
            description: telemetryOptions.Value.ApprovalDecisionDescription);
    }

    public Activity? StartActivity(string name) =>
        _activitySource.CreateActivity(name, ActivityKind.Internal)?.Start();

    public void ListenerRefreshFailed(string listenerId, Exception exception)
    {
        Activity.Current?.AddException(exception);
        _listenerRefreshFailureCounter.Add(
            1,
            new TagList
            {
                { Metrics.ListenerTagName, listenerId },
                { Metrics.ExceptionTypeTagName, exception.GetType().FullName },
            });
    }

    public void FeedRefreshFailed(string listenerId, Exception exception)
    {
        Activity.Current?.AddException(exception);
        _feedRefreshFailureCounter.Add(
            1,
            new TagList
            {
                { Metrics.ListenerTagName, listenerId },
                { Metrics.ExceptionTypeTagName, exception.GetType().FullName },
            });
    }

    public void DispatchFailed(string targetId, Exception exception)
    {
        Activity.Current?.AddException(exception);
        _dispatchFailureCounter.Add(
            1,
            new TagList
            {
                { Metrics.TargetTagName, targetId },
                { Metrics.ExceptionTypeTagName, exception.GetType().FullName },
            });
    }

    public void ApprovalDecided(bool approved) =>
        _approvalDecisionCounter.Add(
            1,
            new TagList
            {
                { Metrics.DecisionTagName, approved ? Metrics.ApprovedDecision : Metrics.RejectedDecision },
            });

    public void Dispose() => _activitySource.Dispose();

    internal static class Metrics
    {
        public const string MeterName = nameof(Sentinel);

        public const string SourceName = nameof(Sentinel);

        public const string ListenerRefreshFailureCounterName = "sentinel.listener_refresh.failures";

        public const string FeedRefreshFailureCounterName = "sentinel.feed_refresh.failures";

        public const string DispatchFailureCounterName = "sentinel.dispatch.failures";

        public const string ApprovalDecisionCounterName = "sentinel.approvals.decisions";

        public const string ListenerTagName = "listener";

        public const string TargetTagName = "target";

        public const string DecisionTagName = "decision";

        public const string ExceptionTypeTagName = "exception.type";

        public const string ApprovedDecision = "approved";

        public const string RejectedDecision = "rejected";
    }
}
