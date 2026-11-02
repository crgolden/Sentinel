namespace Sentinel.Tests.Unit.Listeners;

using A2A;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ListenerCardValidatorTests
{
    [Fact]
    public void Problem_InterfacesOnTheConfiguredOrigin_ReturnsNone()
    {
        // Arrange
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/");
        var card = ListenerFixtures.Card(baseAddress);

        // Act
        var problem = ListenerCardValidator.Problem(card, baseAddress);

        // Assert
        Assert.Equal(ListenerProblem.None, problem);
    }

    [Fact]
    public void Problem_NoInterfaces_ReturnsNoInterfaces()
    {
        // Arrange
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/");
        var card = ListenerFixtures.Card(baseAddress);
        card.SupportedInterfaces = [];

        // Act
        var problem = ListenerCardValidator.Problem(card, baseAddress);

        // Assert
        Assert.Equal(ListenerProblem.NoInterfaces, problem);
    }

    [Fact]
    public void Problem_PlainHttpInterface_ReturnsInsecureInterface()
    {
        // Arrange
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/");
        var card = ListenerFixtures.Card(new Uri($"http://{baseAddress.Host}/"));

        // Act
        var problem = ListenerCardValidator.Problem(card, baseAddress);

        // Assert
        Assert.Equal(ListenerProblem.InsecureInterface, problem);
    }

    [Fact]
    public void Problem_InterfaceOnAnotherHost_ReturnsForeignOrigin()
    {
        // Arrange
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/");
        var card = ListenerFixtures.Card(new Uri($"https://{Generated.NewHostname()}/"));

        // Act
        var problem = ListenerCardValidator.Problem(card, baseAddress);

        // Assert
        Assert.Equal(ListenerProblem.ForeignOrigin, problem);
    }

    [Fact]
    public void Problem_InterfaceOnAnotherPort_ReturnsForeignOrigin()
    {
        // Arrange
        var baseAddress = new Uri($"https://{Generated.NewHostname()}/");
        var card = ListenerFixtures.Card(new UriBuilder(baseAddress) { Port = Generated.NewPortNumber() }.Uri);

        // Act
        var problem = ListenerCardValidator.Problem(card, baseAddress);

        // Assert
        Assert.Equal(ListenerProblem.ForeignOrigin, problem);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SupportsFeeds_ReflectsTheFeedExtension(bool feeds)
    {
        // Arrange
        var card = ListenerFixtures.Card(new Uri($"https://{Generated.NewHostname()}/"), feeds);

        // Act
        var supported = ListenerCardValidator.SupportsFeeds(card);

        // Assert
        Assert.Equal(feeds, supported);
    }

    [Fact]
    public void SupportsFeeds_NoCapabilities_ReturnsFalse()
    {
        // Arrange
        var card = new AgentCard { Name = Generated.LowercaseToken(6), Description = Generated.NewMessageText(), Version = Generated.LowercaseToken(3) };

        // Act
        var supported = ListenerCardValidator.SupportsFeeds(card);

        // Assert
        Assert.False(supported);
    }
}
