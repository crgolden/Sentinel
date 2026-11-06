namespace Sentinel.Tests.Unit.Orchestrator;

using Microsoft.Extensions.AI;
using Moq;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Listeners;
using Sentinel.Orchestrator;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ListenerToolsetTests
{
    private readonly Mock<IListenerDirectory> _directory = new(MockBehavior.Strict);
    private readonly Mock<IListenerRunner> _runner = new(MockBehavior.Strict);
    private readonly OrchestratorOptions _options = Orchestrators.Options();
    private readonly ConversationKey _origin = ConversationKey.Orchestrator(Generated.NewUserId());
    private readonly ListenerToolset _toolset;

    public ListenerToolsetTests() => _toolset = new ListenerToolset(_directory.Object, _runner.Object, _options, _origin);

    [Fact]
    public void Create_WithAMasterBridge_IncludesTheApprovalGatedRelay()
    {
        // Arrange
        _directory.Setup(directory => directory.Listeners).Returns([ListenerFixtures.Online(ListenerFixtures.Definition(ListenerKind.Master))]);

        // Act
        var tools = _toolset.Create();

        // Assert
        Assert.Equal([ListenerToolset.ListListenersName, ListenerToolset.AskListenerName, ListenerToolset.RelayToMasterName], tools.Select(tool => tool.Name));
        Assert.IsType<ApprovalRequiredAIFunction>(tools[2]);
    }

    [Fact]
    public void Create_WithoutAMasterBridge_LeavesOutTheRelay()
    {
        // Arrange
        _directory.Setup(directory => directory.Listeners).Returns([ListenerFixtures.Online(ListenerFixtures.Definition())]);

        // Act
        var tools = _toolset.Create();

        // Assert
        Assert.Equal([ListenerToolset.ListListenersName, ListenerToolset.AskListenerName], tools.Select(tool => tool.Name));
    }

    [Fact]
    public void Create_NeverExposesAnApprovalDecision()
    {
        // Arrange
        _directory.Setup(directory => directory.Listeners).Returns([ListenerFixtures.Online(ListenerFixtures.Definition(ListenerKind.Master))]);

        // Act
        var tools = _toolset.Create();

        // Assert
        Assert.DoesNotContain(tools, tool => tool.Name.Contains("approv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListListeners_SummarizesEveryListener()
    {
        // Arrange
        var online = ListenerFixtures.Online(ListenerFixtures.Definition());
        _directory.Setup(directory => directory.Listeners).Returns([online]);

        // Act
        var summaries = _toolset.ListListeners();

        // Assert
        var summary = Assert.Single(summaries);
        Assert.Equal((online.Id, online.Definition.DisplayName, ListenerState.Online, online.Card?.Description), (summary.Id, summary.DisplayName, summary.State, summary.Description));
        Assert.Equal(online.Card?.Skills.Select(skill => skill.Name), summary.Skills);
    }

    [Fact]
    public async Task AskListenerAsync_UnknownListener_ReportsItWithoutRunningAnything()
    {
        // Arrange
        var listenerId = Generated.LowercaseToken(8);
        _directory.Setup(directory => directory.Find(listenerId)).Returns((ListenerSnapshot?)null);

        // Act
        var result = await _toolset.AskListenerAsync(listenerId, Generated.NewMessageText(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ListenerToolResult(listenerId, RunState.Failed, _options.ListenerNotFound, null), result);
    }

    [Fact]
    public async Task AskListenerAsync_MasterBridge_IsRefusedSoTheRelayStaysApprovalGated()
    {
        // Arrange
        var master = ListenerFixtures.Online(ListenerFixtures.Definition(ListenerKind.Master));
        _directory.Setup(directory => directory.Find(master.Id)).Returns(master);

        // Act
        var result = await _toolset.AskListenerAsync(master.Id, Generated.NewMessageText(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RunState.Failed, result.Status);
    }

    [Fact]
    public async Task AskListenerAsync_ToolListener_RunsATurnLinkedToTheOrchestratorConversation()
    {
        // Arrange
        var listener = ListenerFixtures.Online(ListenerFixtures.Definition());
        var message = Generated.NewMessageText();
        var reply = Generated.NewMessageText();
        var expected = DispatchCommand.Message(new ConversationKey(_origin.OperatorId, listener.Id), message, _origin);
        _directory.Setup(directory => directory.Find(listener.Id)).Returns(listener);
        _runner.Setup(runner => runner.RunAsync(expected, It.IsAny<CancellationToken>())).ReturnsAsync(new ListenerTurnResult(RunState.Completed, reply, null));

        // Act
        var result = await _toolset.AskListenerAsync(listener.Id, message, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ListenerToolResult(listener.Id, RunState.Completed, reply, null), result);
    }

    [Fact]
    public async Task RelayToMasterAsync_NoMaster_ReportsIt()
    {
        // Arrange
        _directory.Setup(directory => directory.Listeners).Returns([]);

        // Act
        var result = await _toolset.RelayToMasterAsync(Generated.NewMessageText(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_options.MasterNotConfigured, result.Text);
    }

    [Fact]
    public async Task RelayToMasterAsync_Master_RunsATurnOnTheMasterBridge()
    {
        // Arrange
        var master = ListenerFixtures.Online(ListenerFixtures.Definition(ListenerKind.Master));
        var message = Generated.NewMessageText();
        var approval = ApprovalFixtures.Pending();
        _directory.Setup(directory => directory.Listeners).Returns([master]);
        _runner.Setup(runner => runner.RunAsync(DispatchCommand.Message(new ConversationKey(_origin.OperatorId, master.Id), message, _origin), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListenerTurnResult(RunState.AwaitingApproval, null, approval));

        // Act
        var result = await _toolset.RelayToMasterAsync(message, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ListenerToolResult(master.Id, RunState.AwaitingApproval, null, approval.Id), result);
    }
}
