namespace Sentinel.Tests.Unit.Listeners;

using Moq;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ListenerAgentCacheTests
{
    private readonly Mock<IListenerDirectory> _directory = new(MockBehavior.Strict);
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new(MockBehavior.Strict);
    private readonly ListenerAgentCache _cache;

    public ListenerAgentCacheTests()
    {
        _httpClientFactory.Setup(factory => factory.CreateClient(ListenerHttpClientNames.A2A)).Returns(() => new HttpClient());
        _cache = new ListenerAgentCache(_directory.Object, _httpClientFactory.Object);
    }

    [Fact]
    public void GetAgent_OfflineListener_ReturnsNull()
    {
        // Arrange
        var snapshot = ListenerFixtures.Offline(ListenerFixtures.Definition());
        _directory.Setup(directory => directory.Find(snapshot.Id)).Returns(snapshot);

        // Act
        var agent = _cache.GetAgent(snapshot.Id);

        // Assert
        Assert.Null(agent);
    }

    [Fact]
    public void GetAgent_OnlineListener_ReturnsAnAgentNamedForTheListener()
    {
        // Arrange
        var snapshot = ListenerFixtures.Online(ListenerFixtures.Definition());
        _directory.Setup(directory => directory.Find(snapshot.Id)).Returns(snapshot);

        // Act
        var agent = _cache.GetAgent(snapshot.Id);

        // Assert
        Assert.Equal(snapshot.Definition.DisplayName, agent?.Name);
        Assert.Equal(snapshot.Id, agent?.Id);
    }

    [Fact]
    public void GetAgent_SameCardTwice_ReusesTheAgent()
    {
        // Arrange
        var snapshot = ListenerFixtures.Online(ListenerFixtures.Definition());
        _directory.Setup(directory => directory.Find(snapshot.Id)).Returns(snapshot);
        var first = _cache.GetAgent(snapshot.Id);

        // Act
        var second = _cache.GetAgent(snapshot.Id);

        // Assert
        Assert.Same(first, second);
        _httpClientFactory.Verify(factory => factory.CreateClient(ListenerHttpClientNames.A2A), Times.Once);
    }

    [Fact]
    public void GetAgent_RefreshedCard_BuildsANewAgent()
    {
        // Arrange
        var definition = ListenerFixtures.Definition();
        var snapshot = ListenerFixtures.Online(definition);
        _directory.SetupSequence(directory => directory.Find(definition.Id)).Returns(snapshot).Returns(ListenerFixtures.Online(definition));
        var first = _cache.GetAgent(definition.Id);

        // Act
        var second = _cache.GetAgent(definition.Id);

        // Assert
        Assert.NotSame(first, second);
    }
}
