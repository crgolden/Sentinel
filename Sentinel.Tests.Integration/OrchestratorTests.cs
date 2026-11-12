namespace Sentinel.Tests.Integration;

using Microsoft.Extensions.DependencyInjection;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Listeners;
using Sentinel.Orchestrator;
using Sentinel.Tests.Integration.TestSupport;

[Collection(IntegrationConstants.CollectionName)]
[Trait("Category", "Integration")]
public sealed class OrchestratorTests(SentinelWebApplicationFactory factory)
{
    private IDispatchQueue Queue => factory.Services.GetRequiredService<IDispatchQueue>();

    private IConversationReader Reader => factory.Services.GetRequiredService<IConversationReader>();

    private IApprovalService Approvals => factory.Services.GetRequiredService<IApprovalService>();

    [Fact]
    public async Task Message_WhereTheModelAsksAListener_RelaysTheListenersReplyIntoTheAnswer()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);
        var operatorId = Generated.NewUserId();
        var conversation = ConversationKey.Orchestrator(operatorId);
        var question = Generated.NewMessageText();
        var answer = Generated.NewMessageText();
        factory.ChatClient.CallTool(ListenerToolset.AskListenerName, new Dictionary<string, object?> { [ListenerToolset.ListenerIdArgument] = factory.ToolListenerId, [ListenerToolset.MessageArgument] = question });
        factory.ChatClient.Reply(answer);
        using var waiter = factory.Wait(conversation);

        await Queue.EnqueueAsync(DispatchCommand.Message(conversation, Generated.NewMessageText()), TestContext.Current.CancellationToken);

        Assert.Equal(RunState.Completed, await waiter.Settled);
        Assert.Equal(answer, (await Reader.GetTranscriptAsync(conversation))[^1].Text);
        var listenerTranscript = await Reader.GetTranscriptAsync(new ConversationKey(operatorId, factory.ToolListenerId));
        Assert.Equal([question, $"{IntegrationConstants.EchoPrefix}{question}"], listenerTranscript.Select(entry => entry.Text));
    }

    [Fact]
    public async Task Message_WhereTheModelRelaysToTheMaster_WaitsForTheOperatorThenRelays()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);
        var operatorId = Generated.NewUserId();
        var conversation = ConversationKey.Orchestrator(operatorId);
        var relayed = Generated.NewMessageText();
        var answer = Generated.NewMessageText();
        factory.ChatClient.CallTool(ListenerToolset.RelayToMasterName, new Dictionary<string, object?> { [ListenerToolset.MessageArgument] = relayed });
        using (var requestWaiter = factory.Wait(conversation))
        {
            await Queue.EnqueueAsync(DispatchCommand.Message(conversation, Generated.NewMessageText()), TestContext.Current.CancellationToken);
            Assert.Equal(RunState.AwaitingApproval, await requestWaiter.Settled);
        }

        var pending = Assert.Single(await Approvals.GetPendingAsync(operatorId));
        Assert.Equal(ApprovalKind.OrchestratorTool, pending.Kind);
        Assert.DoesNotContain(relayed, factory.MasterListener.Handler.ReceivedText);
        factory.ChatClient.Reply(answer);
        using var decisionWaiter = factory.Wait(conversation);

        await Approvals.DecideAsync(operatorId, pending.Id, true, null, TestContext.Current.CancellationToken);

        Assert.Equal(RunState.Completed, await decisionWaiter.Settled);
        Assert.Contains(relayed, factory.MasterListener.Handler.ReceivedText);
        Assert.Equal(answer, (await Reader.GetTranscriptAsync(conversation))[^1].Text);
    }
}
