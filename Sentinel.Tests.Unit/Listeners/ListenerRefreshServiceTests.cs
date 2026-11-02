namespace Sentinel.Tests.Unit.Listeners;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ListenerRefreshServiceTests : IDisposable
{
    private readonly TelemetryHarness _telemetry = new();
    private readonly FakeTimeProvider _timeProvider = new(Generated.NewUtcTimestamp());
    private readonly Mock<IAgentCardSource> _cardSource = new(MockBehavior.Strict);
    private readonly ListenerDefinition _definition = ListenerFixtures.Definition();
    private readonly ListenerRegistry _registry;
    private readonly ListenerRefreshService _service;

    public ListenerRefreshServiceTests()
    {
        _registry = new ListenerRegistry([_definition]);
        _service = new ListenerRefreshService(
            _registry,
            _cardSource.Object,
            _timeProvider,
            Options.Create(new ListenerRefreshOptions(Generated.NewCount(), Generated.NewCount())),
            _telemetry.Telemetry);
    }

    [Fact]
    public async Task RefreshAll_ValidCard_MarksTheListenerOnlineAtTheCurrentTime()
    {
        // Arrange
        var card = ListenerFixtures.Card(_definition.BaseAddress);
        _cardSource.Setup(source => source.GetCardAsync(_definition.BaseAddress, It.IsAny<CancellationToken>())).ReturnsAsync(card);

        // Act
        await _service.RefreshAllAsync(TestContext.Current.CancellationToken);

        // Assert
        var snapshot = _registry.Find(_definition.Id);
        Assert.Equal(ListenerState.Online, snapshot?.State);
        Assert.Same(card, snapshot?.Card);
        Assert.True(snapshot?.FeedsSupported);
        Assert.Equal(_timeProvider.GetUtcNow(), snapshot?.LastCheckedAt);
    }

    [Fact]
    public async Task RefreshAll_CardOnAnotherOrigin_MarksTheListenerMisconfiguredWithoutKeepingTheCard()
    {
        // Arrange
        var card = ListenerFixtures.Card(new Uri($"https://{Generated.NewHostname()}/"));
        _cardSource.Setup(source => source.GetCardAsync(_definition.BaseAddress, It.IsAny<CancellationToken>())).ReturnsAsync(card);

        // Act
        await _service.RefreshAllAsync(TestContext.Current.CancellationToken);

        // Assert
        var snapshot = _registry.Find(_definition.Id);
        Assert.Equal(ListenerState.Misconfigured, snapshot?.State);
        Assert.Equal(ListenerProblem.ForeignOrigin, snapshot?.Problem);
        Assert.Null(snapshot?.Card);
    }

    [Fact]
    public async Task RefreshAll_CardFetchFails_MarksTheListenerOfflineAndCountsTheFailure()
    {
        // Arrange
        using var capture = _telemetry.Capture(Telemetry.Metrics.ListenerRefreshFailureCounterName);
        _cardSource.Setup(source => source.GetCardAsync(_definition.BaseAddress, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());

        // Act
        await _service.RefreshAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ListenerState.Offline, _registry.Find(_definition.Id)?.State);
        Assert.Equal(_definition.Id, Assert.Single(capture.Measurements).Tags[Telemetry.Metrics.ListenerTagName]);
    }

    [Fact]
    public async Task ExecuteAsync_OnStart_RefreshesBeforeTheFirstTick()
    {
        // Arrange
        var fetched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _cardSource.Setup(source => source.GetCardAsync(_definition.BaseAddress, It.IsAny<CancellationToken>()))
            .Callback(fetched.SetResult)
            .ReturnsAsync(ListenerFixtures.Card(_definition.BaseAddress));

        // Act
        await _service.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await fetched.Task.WaitAsync(TestContext.Current.CancellationToken);
        await _service.StopAsync(TestContext.Current.CancellationToken);
        _cardSource.Verify(source => source.GetCardAsync(_definition.BaseAddress, It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        _service.Dispose();
        _telemetry.Dispose();
    }
}
