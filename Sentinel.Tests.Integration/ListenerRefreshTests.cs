namespace Sentinel.Tests.Integration;

using Microsoft.Extensions.DependencyInjection;
using Sentinel.Listeners;
using Sentinel.Tests.Integration.TestSupport;

[Collection(IntegrationConstants.CollectionName)]
[Trait("Category", "Integration")]
public sealed class ListenerRefreshTests(SentinelWebApplicationFactory factory)
{
    [Fact]
    public async Task RefreshAll_ReachableListenerWithSameOriginCard_IsOnlineWithFeeds()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        var snapshot = factory.Services.GetRequiredService<IListenerDirectory>().Find(factory.ToolListenerId);
        Assert.Equal(ListenerState.Online, snapshot?.State);
        Assert.True(snapshot?.FeedsSupported);
    }

    [Fact]
    public async Task RefreshAll_CardPointingAtAnotherOrigin_IsMisconfigured()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        var snapshot = factory.Services.GetRequiredService<IListenerDirectory>().Find(factory.MisconfiguredListenerId);
        Assert.Equal(ListenerState.Misconfigured, snapshot?.State);
        Assert.Equal(ListenerProblem.ForeignOrigin, snapshot?.Problem);
    }

    [Fact]
    public async Task RefreshAll_UnreachableListener_IsOffline()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);

        var snapshot = factory.Services.GetRequiredService<IListenerDirectory>().Find(factory.OfflineListenerId);
        Assert.Equal(ListenerState.Offline, snapshot?.State);
        Assert.Equal(ListenerProblem.Unreachable, snapshot?.Problem);
    }
}
