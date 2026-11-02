namespace Sentinel.Feeds;

using Microsoft.Extensions.Options;
using OpenTelemetry;
using Sentinel.Listeners;

public sealed class FeedRefreshService(
    IListenerDirectory directory,
    IFeedClient feedClient,
    IFeedCache feedCache,
    TimeProvider timeProvider,
    IOptions<FeedRefreshOptions> options,
    Telemetry telemetry) : BackgroundService
{
    internal static IReadOnlyList<FeedKind> KindsFor(ListenerKind kind) =>
        kind == ListenerKind.Master ? [FeedKind.Status, FeedKind.Artifacts, FeedKind.Board] : [FeedKind.Status, FeedKind.Artifacts];

    internal async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        foreach (var snapshot in directory.Listeners.Where(listener => listener is { State: ListenerState.Online, FeedsSupported: true }))
        {
            await RefreshAsync(snapshot.Definition, cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAllAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(ListenerDefinition listener, CancellationToken cancellationToken)
    {
        foreach (var kind in KindsFor(listener.Kind))
        {
            try
            {
                using (SuppressInstrumentationScope.Begin())
                {
                    var json = await feedClient.GetAsync(listener, kind, cancellationToken);
                    await feedCache.StoreAsync(listener.Id, kind, json);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                telemetry.FeedRefreshFailed(listener.Id, ex);
            }
        }
    }
}
