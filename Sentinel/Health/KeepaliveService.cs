namespace Sentinel.Health;

using System.Diagnostics;
using OpenTelemetry;

public sealed class KeepaliveService(HttpClient httpClient, KeepaliveOptions options, TimeProvider timeProvider) : BackgroundService
{
    public static Uri PingUri(string hostname) => new($"https://{hostname}{EndpointRoutes.Ping}", UriKind.Absolute);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using (SuppressInstrumentationScope.Begin())
                {
                    using var response = await httpClient.GetAsync(options.PingUri, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Activity.Current?.AddException(ex);
            }
        }
    }
}
