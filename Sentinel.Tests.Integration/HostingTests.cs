namespace Sentinel.Tests.Integration;

using System.Net;
using Sentinel.Tests.Integration.TestSupport;

[Collection(IntegrationConstants.CollectionName)]
[Trait("Category", "Integration")]
public sealed class HostingTests(SentinelWebApplicationFactory factory)
{
    [Fact]
    public async Task Health_ReturnsExactlyHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ping_ReturnsOkWithoutSignIn()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_WithoutSignIn_IsChallenged()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_SignedInWithoutOperatorRole_IsForbidden()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(IntegrationConstants.OperatorHeader, Generated.NewUserId().ToString());

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_SignedInOperator_RendersEveryListenerTile()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(IntegrationConstants.OperatorHeader, Generated.NewUserId().ToString());
        client.DefaultRequestHeaders.Add(IntegrationConstants.RoleHeader, AuthorizationPolicies.OperatorRole);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains($"id=\"listener-tile-{factory.ToolListenerId}\"", body, StringComparison.Ordinal);
        Assert.Contains($"id=\"listener-tile-{factory.MasterListenerId}\"", body, StringComparison.Ordinal);
    }
}
