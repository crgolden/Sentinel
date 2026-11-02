namespace Sentinel.Tests.Integration;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Feeds;
using Sentinel.Listeners;
using Sentinel.Tests.Integration.TestSupport;

[Collection(IntegrationConstants.CollectionName)]
[Trait("Category", "Integration")]
public sealed class FeedRefreshTests(SentinelWebApplicationFactory factory)
{
    [Fact]
    public async Task RefreshAll_OnlineListeners_CachesStatusArtifactsAndTheMastersBoard()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        await factory.Hosted<FeedRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        var reader = factory.Services.GetRequiredService<IFeedReader>();
        var expectedStatus = JsonDocument.Parse(factory.Feeds.Status).RootElement.GetProperty("summary").GetString();
        Assert.Equal(expectedStatus, (await reader.GetStatusAsync(factory.ToolListenerId))?.Summary);
        Assert.Single((await reader.GetArtifactsAsync(factory.ToolListenerId))?.Items ?? []);
        Assert.Single((await reader.GetBoardAsync(factory.MasterListenerId))?.Sessions ?? []);
        Assert.Null(await reader.GetBoardAsync(factory.ToolListenerId));
    }

    [Fact]
    public async Task RefreshAll_FeedRequests_CarryTheClientCredentialsToken()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        await factory.Hosted<FeedRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, factory.ToolListener.UnauthorizedRequests);
        Assert.True(factory.TokenEndpoint.Requests > 0);
    }
}
