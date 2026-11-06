namespace Sentinel.Tests.Unit.Health;

using System.Net;
using Sentinel.Health;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class KeepaliveServiceTests
{
    [Fact]
    public void PingUri_PointsAtThePingEndpointOverHttps()
    {
        // Arrange
        var hostname = Generated.NewHostname();

        // Act
        var uri = KeepaliveService.PingUri(hostname);

        // Assert
        Assert.Equal(new Uri($"https://{hostname}/ping"), uri);
    }

    [Fact]
    public async Task ExecuteAsync_AfterTheInterval_PingsTheSite()
    {
        // Arrange
        var interval = TimeSpan.FromMinutes(Generated.NewCount());
        var pingUri = KeepaliveService.PingUri(Generated.NewHostname());
        var timeProvider = new TimerSignalingTimeProvider();
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, Generated.NewMessageText());
        using var service = new KeepaliveService(new HttpClient(handler), new KeepaliveOptions(pingUri, interval), timeProvider);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await timeProvider.FirstTimerCreated.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        timeProvider.Advance(interval);

        // Assert
        await handler.FirstRequest.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(pingUri, handler.Requests[0].RequestUri);
    }

    [Fact]
    public async Task ExecuteAsync_PingFails_KeepsRunning()
    {
        // Arrange
        var interval = TimeSpan.FromMinutes(Generated.NewCount());
        var timeProvider = new TimerSignalingTimeProvider();
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException());
        using var service = new KeepaliveService(new HttpClient(handler), new KeepaliveOptions(KeepaliveService.PingUri(Generated.NewHostname()), interval), timeProvider);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await timeProvider.FirstTimerCreated.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        timeProvider.Advance(interval);

        // Assert
        await handler.FirstRequest.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(service.ExecuteTask?.IsFaulted);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }
}
