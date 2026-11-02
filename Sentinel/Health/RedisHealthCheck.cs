namespace Sentinel.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

public sealed class RedisHealthCheck(IDatabase database) : IHealthCheck
{
    public const string Name = "redis";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await database.PingAsync();
            return HealthCheckResult.Healthy(latency.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (RedisException ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
