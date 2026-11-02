namespace Sentinel.Tests.Unit.Listeners;

using System.Net;
using System.Text.Json;
using A2A;
using Microsoft.Extensions.Options;
using Moq;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class A2AAgentCardSourceTests
{
    [Fact]
    public async Task GetCardAsync_FetchesTheWellKnownCardFromTheBaseAddress()
    {
        // Arrange
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/");
        var card = ListenerFixtures.Card(baseAddress);
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, JsonSerializer.Serialize(card, A2AJsonUtilities.DefaultOptions));
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory.Setup(factory => factory.CreateClient(ListenerHttpClientNames.Cards)).Returns(new HttpClient(handler));
        var source = new A2AAgentCardSource(httpClientFactory.Object, Options.Create(new ListenerRefreshOptions(Generated.NewCount(), 1_048_576)));

        // Act
        var fetched = await source.GetCardAsync(baseAddress, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((card.Name, card.Version), (fetched.Name, fetched.Version));
        Assert.Equal(new Uri(baseAddress, ".well-known/agent-card.json"), Assert.Single(handler.Requests).RequestUri);
    }
}
