namespace Sentinel.Tests.Unit;

using System.Diagnostics;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class TelemetryTests : IDisposable
{
    private readonly TelemetryHarness _harness = new();

    [Fact]
    public void Counters_CarryTheConfiguredDescriptions()
    {
        // Arrange
        Dictionary<string, string?> expected = new(StringComparer.Ordinal)
        {
            [Telemetry.Metrics.ListenerRefreshFailureCounterName] = _harness.Descriptions.ListenerRefreshFailureDescription,
            [Telemetry.Metrics.FeedRefreshFailureCounterName] = _harness.Descriptions.FeedRefreshFailureDescription,
            [Telemetry.Metrics.DispatchFailureCounterName] = _harness.Descriptions.DispatchFailureDescription,
            [Telemetry.Metrics.ApprovalDecisionCounterName] = _harness.Descriptions.ApprovalDecisionDescription,
        };

        // Act
        var actual = _harness.PublishedDescriptions();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ListenerRefreshFailed_CountsOneFailureTaggedWithTheListenerAndExceptionType()
    {
        // Arrange
        using var capture = _harness.Capture(Telemetry.Metrics.ListenerRefreshFailureCounterName);
        var listenerId = Generated.LowercaseToken(8);

        // Act
        _harness.Telemetry.ListenerRefreshFailed(listenerId, new TimeoutException());

        // Assert
        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal(listenerId, measurement.Tags[Telemetry.Metrics.ListenerTagName]);
        Assert.Equal(typeof(TimeoutException).FullName, measurement.Tags[Telemetry.Metrics.ExceptionTypeTagName]);
    }

    [Fact]
    public void FeedRefreshFailed_CountsOneFailureTaggedWithTheListener()
    {
        // Arrange
        using var capture = _harness.Capture(Telemetry.Metrics.FeedRefreshFailureCounterName);
        var listenerId = Generated.LowercaseToken(8);

        // Act
        _harness.Telemetry.FeedRefreshFailed(listenerId, new HttpRequestException());

        // Assert
        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(listenerId, measurement.Tags[Telemetry.Metrics.ListenerTagName]);
        Assert.Equal(typeof(HttpRequestException).FullName, measurement.Tags[Telemetry.Metrics.ExceptionTypeTagName]);
    }

    [Fact]
    public void DispatchFailed_CountsOneFailureTaggedWithTheTarget()
    {
        // Arrange
        using var capture = _harness.Capture(Telemetry.Metrics.DispatchFailureCounterName);
        var targetId = Generated.LowercaseToken(8);

        // Act
        _harness.Telemetry.DispatchFailed(targetId, new InvalidOperationException());

        // Assert
        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(targetId, measurement.Tags[Telemetry.Metrics.TargetTagName]);
    }

    [Theory]
    [InlineData(true, Telemetry.Metrics.ApprovedDecision)]
    [InlineData(false, Telemetry.Metrics.RejectedDecision)]
    public void ApprovalDecided_TagsTheDecision(bool approved, string decision)
    {
        // Arrange
        using var capture = _harness.Capture(Telemetry.Metrics.ApprovalDecisionCounterName);

        // Act
        _harness.Telemetry.ApprovalDecided(approved);

        // Assert
        Assert.Equal(decision, Assert.Single(capture.Measurements).Tags[Telemetry.Metrics.DecisionTagName]);
    }

    [Fact]
    public void StartActivity_StartsAnInternalSpanOnTheSentinelSource()
    {
        // Arrange
        using var listener = ListenToSentinel();
        var name = Generated.LowercaseToken(8);

        // Act
        using var activity = _harness.Telemetry.StartActivity(name);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(Telemetry.Metrics.SourceName, activity.Source.Name);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal(name, activity.OperationName);
    }

    [Fact]
    public void StartActivity_StartsNoSpanOnceTheTelemetryIsDisposed()
    {
        // Arrange
        using var listener = ListenToSentinel();
        _harness.Telemetry.Dispose();

        // Act
        using var activity = _harness.Telemetry.StartActivity(Generated.LowercaseToken(8));

        // Assert
        Assert.Null(activity);
    }

    public void Dispose() => _harness.Dispose();

    private static ActivityListener ListenToSentinel()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, Telemetry.Metrics.SourceName, StringComparison.Ordinal),
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
