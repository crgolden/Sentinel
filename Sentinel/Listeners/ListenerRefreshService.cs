namespace Sentinel.Listeners;

using Microsoft.Extensions.Options;
using OpenTelemetry;

public sealed class ListenerRefreshService(
    ListenerRegistry registry,
    IAgentCardSource cardSource,
    TimeProvider timeProvider,
    IOptions<ListenerRefreshOptions> options,
    Telemetry telemetry) : BackgroundService
{
    internal async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        foreach (var definition in registry.Definitions)
        {
            registry.Update(await RefreshAsync(definition, cancellationToken));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds), timeProvider);
        do
        {
            await RefreshAllAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<ListenerSnapshot> RefreshAsync(ListenerDefinition definition, CancellationToken cancellationToken)
    {
        var checkedAt = timeProvider.GetUtcNow();
        try
        {
            using (SuppressInstrumentationScope.Begin())
            {
                var card = await cardSource.GetCardAsync(definition.BaseAddress, cancellationToken);
                var problem = ListenerCardValidator.Problem(card, definition.BaseAddress);
                return problem == ListenerProblem.None
                    ? new ListenerSnapshot(definition, ListenerState.Online, problem, card, checkedAt, ListenerCardValidator.SupportsFeeds(card))
                    : new ListenerSnapshot(definition, ListenerState.Misconfigured, problem, null, checkedAt, false);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            telemetry.ListenerRefreshFailed(definition.Id, ex);
            return new ListenerSnapshot(definition, ListenerState.Offline, ListenerProblem.Unreachable, null, checkedAt, false);
        }
    }
}
