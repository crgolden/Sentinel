namespace Sentinel.Tests.Integration.TestSupport;

[CollectionDefinition(IntegrationConstants.CollectionName)]
public sealed class IntegrationCollection : ICollectionFixture<SentinelWebApplicationFactory>
{
}
