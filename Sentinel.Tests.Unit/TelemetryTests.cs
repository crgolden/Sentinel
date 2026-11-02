namespace Sentinel.Tests.Unit;

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

    public void Dispose() => _harness.Dispose();
}
