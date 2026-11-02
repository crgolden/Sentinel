namespace Sentinel.Tests.Unit.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Sentinel.Health;
using StackExchange.Redis;

[Trait("Category", "Unit")]
public sealed class RedisHealthCheckTests
{
    private readonly Mock<IDatabase> _database = new(MockBehavior.Strict);

    [Fact]
    public async Task CheckHealthAsync_PingSucceeds_IsHealthy()
    {
        // Arrange
        _database.Setup(database => database.PingAsync(CommandFlags.None)).ReturnsAsync(TimeSpan.FromMilliseconds(Generated.NewCount()));
        var check = new RedisHealthCheck(_database.Object);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_PingThrows_IsUnhealthy()
    {
        // Arrange
        var exception = new RedisException(Generated.NewErrorMessage());
        _database.Setup(database => database.PingAsync(CommandFlags.None)).ThrowsAsync(exception);
        var check = new RedisHealthCheck(_database.Object);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((HealthStatus.Unhealthy, exception), (result.Status, result.Exception));
    }
}
