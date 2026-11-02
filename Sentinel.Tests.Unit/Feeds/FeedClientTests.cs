namespace Sentinel.Tests.Unit.Feeds;

using System.Net;
using Microsoft.Extensions.Options;
using Moq;
using Sentinel.Feeds;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class FeedClientTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new(MockBehavior.Strict);
    private readonly ListenerDefinition _listener = ListenerFixtures.Definition();

    [Theory]
    [InlineData(FeedKind.Status, FeedContract.StatusPath)]
    [InlineData(FeedKind.Artifacts, FeedContract.ArtifactsPath)]
    [InlineData(FeedKind.Board, FeedContract.BoardPath)]
    public void FeedUri_BaseWithTrailingSlash_AppendsTheFeedPath(FeedKind kind, string path)
    {
        // Arrange
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/{Generated.LowercaseToken(5)}/");

        // Act
        var uri = FeedClient.FeedUri(baseAddress, kind);

        // Assert
        Assert.Equal(new Uri($"{baseAddress.AbsoluteUri}{path}"), uri);
    }

    [Fact]
    public void FeedUri_BaseWithoutTrailingSlash_KeepsTheBasePath()
    {
        // Arrange
        var prefix = Generated.LowercaseToken(5);
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/{prefix}");

        // Act
        var uri = FeedClient.FeedUri(baseAddress, FeedKind.Status);

        // Assert
        Assert.Equal($"/{prefix}/{FeedContract.StatusPath}", uri.AbsolutePath);
    }

    [Fact]
    public async Task GetAsync_SuccessfulResponse_ReturnsTheBodyFromTheFeedUri()
    {
        // Arrange
        var body = Generated.NewMessageText();
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, body);
        var client = Client(handler, body.Length);

        // Act
        var actual = await client.GetAsync(_listener, FeedKind.Artifacts, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(body, actual);
        Assert.Equal(FeedClient.FeedUri(_listener.BaseAddress, FeedKind.Artifacts), Assert.Single(handler.Requests).RequestUri);
    }

    [Fact]
    public async Task GetAsync_BodyLargerThanTheLimit_Throws()
    {
        // Arrange
        var body = Generated.NewMessageText();
        var client = Client(StubHttpMessageHandler.Responding(HttpStatusCode.OK, body), body.Length - 1);

        // Act
        var exception = await Record.ExceptionAsync(() => client.GetAsync(_listener, FeedKind.Status, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task GetAsync_ErrorStatus_Throws()
    {
        // Arrange
        var client = Client(StubHttpMessageHandler.Responding(Generated.NewServerErrorStatusCode(), Generated.NewMessageText()), Generated.NewCount());

        // Act
        var exception = await Record.ExceptionAsync(() => client.GetAsync(_listener, FeedKind.Status, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<HttpRequestException>(exception);
    }

    private FeedClient Client(StubHttpMessageHandler handler, int maxFeedBytes)
    {
        _httpClientFactory.Setup(factory => factory.CreateClient(ListenerHttpClientNames.Feeds)).Returns(new HttpClient(handler));
        return new FeedClient(_httpClientFactory.Object, Options.Create(new FeedRefreshOptions(Generated.NewCount(), maxFeedBytes)));
    }
}
