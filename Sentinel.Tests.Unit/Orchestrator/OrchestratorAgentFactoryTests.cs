namespace Sentinel.Tests.Unit.Orchestrator;

using Microsoft.Extensions.AI;
using Moq;
using Sentinel.Listeners;
using Sentinel.Orchestrator;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class OrchestratorAgentFactoryTests
{
    [Fact]
    public void Create_NamesTheAgentForTheOrchestratorTarget()
    {
        // Arrange
        var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
        chatClient.Setup(client => client.GetService(It.IsAny<Type>(), It.IsAny<object?>())).Returns((object?)null);
        var factory = new OrchestratorAgentFactory(chatClient.Object, Microsoft.Extensions.Options.Options.Create(Orchestrators.Options()));

        // Act
        var agent = factory.Create();

        // Assert
        Assert.Equal(ListenerCatalog.OrchestratorTargetId, agent.Name);
    }
}
