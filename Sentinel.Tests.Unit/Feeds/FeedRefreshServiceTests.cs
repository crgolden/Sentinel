namespace Sentinel.Tests.Unit.Feeds;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Sentinel.Feeds;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class FeedRefreshServiceTests : IDisposable
{
    private readonly TelemetryHarness _telemetry = new();
    private readonly Mock<IListenerDirectory> _directory = new(MockBehavior.Strict);
    private readonly Mock<IFeedClient> _client = new(MockBehavior.Strict);
    private readonly Mock<IFeedCache> _cache = new(MockBehavior.Strict);
    private readonly FeedRefreshService _service;

    public FeedRefreshServiceTests() =>
        _service = new FeedRefreshService(
            _directory.Object,
            _client.Object,
            _cache.Object,
            new FakeTimeProvider(),
            Options.Create(new FeedRefreshOptions(Generated.NewCount(), Generated.NewCount())),
            _telemetry.Telemetry);

    [Fact]
    public void KindsFor_Master_IncludesTheBoard()
    {
        // Act
        var kinds = FeedRefreshService.KindsFor(ListenerKind.Master);

        // Assert
        Assert.Equal([FeedKind.Status, FeedKind.Artifacts, FeedKind.Board], kinds);
    }

    [Fact]
    public void KindsFor_Tool_ExcludesTheBoard()
    {
        // Act
        var kinds = FeedRefreshService.KindsFor(ListenerKind.Tool);

        // Assert
        Assert.Equal([FeedKind.Status, FeedKind.Artifacts], kinds);
    }

    [Fact]
    public async Task RefreshAll_OnlineListenerWithFeeds_CachesEachFeed()
    {
        // Arrange
        var online = ListenerFixtures.Online(ListenerFixtures.Definition());
        var offline = ListenerFixtures.Offline(ListenerFixtures.Definition());
        var json = Generated.NewMessageText();
        _directory.Setup(directory => directory.Listeners).Returns([online, offline]);
        _client.Setup(client => client.GetAsync(online.Definition, It.IsAny<FeedKind>(), It.IsAny<CancellationToken>())).ReturnsAsync(json);
        _cache.Setup(cache => cache.StoreAsync(online.Id, It.IsAny<FeedKind>(), json)).Returns(Task.CompletedTask);

        // Act
        await _service.RefreshAllAsync(TestContext.Current.CancellationToken);

        // Assert
        _cache.Verify(cache => cache.StoreAsync(online.Id, FeedKind.Status, json), Times.Once);
        _cache.Verify(cache => cache.StoreAsync(online.Id, FeedKind.Artifacts, json), Times.Once);
        _client.Verify(client => client.GetAsync(offline.Definition, It.IsAny<FeedKind>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAll_OneFeedFails_CountsTheFailureAndStillRefreshesTheRest()
    {
        // Arrange
        using var capture = _telemetry.Capture(Telemetry.Metrics.FeedRefreshFailureCounterName);
        var online = ListenerFixtures.Online(ListenerFixtures.Definition());
        var json = Generated.NewMessageText();
        _directory.Setup(directory => directory.Listeners).Returns([online]);
        _client.Setup(client => client.GetAsync(online.Definition, FeedKind.Status, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        _client.Setup(client => client.GetAsync(online.Definition, FeedKind.Artifacts, It.IsAny<CancellationToken>())).ReturnsAsync(json);
        _cache.Setup(cache => cache.StoreAsync(online.Id, FeedKind.Artifacts, json)).Returns(Task.CompletedTask);

        // Act
        await _service.RefreshAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(capture.Measurements);
        _cache.Verify(cache => cache.StoreAsync(online.Id, FeedKind.Artifacts, json), Times.Once);
    }

    public void Dispose()
    {
        _service.Dispose();
        _telemetry.Dispose();
    }
}
