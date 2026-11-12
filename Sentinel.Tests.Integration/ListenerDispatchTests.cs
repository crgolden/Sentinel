namespace Sentinel.Tests.Integration;

using Microsoft.Extensions.DependencyInjection;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Listeners;
using Sentinel.Tests.Integration.TestSupport;

[Collection(IntegrationConstants.CollectionName)]
[Trait("Category", "Integration")]
public sealed class ListenerDispatchTests(SentinelWebApplicationFactory factory)
{
    private IDispatchQueue Queue => factory.Services.GetRequiredService<IDispatchQueue>();

    private IConversationReader Reader => factory.Services.GetRequiredService<IConversationReader>();

    private IApprovalService Approvals => factory.Services.GetRequiredService<IApprovalService>();

    [Fact]
    public async Task Message_ToOnlineListener_RecordsTheOperatorTurnAndTheReply()
    {
        var conversation = await ConversationAsync();
        var message = Generated.NewMessageText();
        using var waiter = factory.Wait(conversation);

        await Queue.EnqueueAsync(DispatchCommand.Message(conversation, message), TestContext.Current.CancellationToken);

        Assert.Equal(RunState.Completed, await waiter.Settled);
        var transcript = await Reader.GetTranscriptAsync(conversation);
        Assert.Equal([message, $"{IntegrationConstants.EchoPrefix}{message}"], transcript.Select(entry => entry.Text));
        Assert.Contains(conversation.OperatorId.ToString(), factory.ToolListener.Handler.ReceivedOperators);
    }

    [Fact]
    public async Task Message_AfterACompletedTask_StartsANewTaskInTheSameContext()
    {
        var conversation = await ConversationAsync();
        var first = Generated.NewMessageText();
        var second = Generated.NewMessageText();
        using (var firstWaiter = factory.Wait(conversation))
        {
            await Queue.EnqueueAsync(DispatchCommand.Message(conversation, first), TestContext.Current.CancellationToken);
            await firstWaiter.Settled;
        }

        using var secondWaiter = factory.Wait(conversation);

        await Queue.EnqueueAsync(DispatchCommand.Message(conversation, second), TestContext.Current.CancellationToken);

        Assert.Equal(RunState.Completed, await secondWaiter.Settled);
        Assert.Equal($"{IntegrationConstants.EchoPrefix}{second}", (await Reader.GetTranscriptAsync(conversation))[^1].Text);
    }

    [Fact]
    public async Task Message_RequiringApproval_WaitsAndRecordsAPendingApproval()
    {
        var conversation = await ConversationAsync();
        var tool = Generated.LowercaseToken(8);
        using var waiter = factory.Wait(conversation);

        await Queue.EnqueueAsync(DispatchCommand.Message(conversation, $"{IntegrationConstants.ApprovalPrefix} {tool}"), TestContext.Current.CancellationToken);

        Assert.Equal(RunState.AwaitingApproval, await waiter.Settled);
        var pending = Assert.Single(await Approvals.GetPendingAsync(conversation.OperatorId));
        Assert.Equal(tool, pending.Tool);
        Assert.Equal(ApprovalRisk.High, pending.Risk);
        Assert.Equal(ApprovalKind.ListenerInput, pending.Kind);
    }

    [Theory]
    [InlineData(true, IntegrationConstants.ApprovedReply, AuditAction.ApprovalGranted)]
    [InlineData(false, IntegrationConstants.RejectedReply, AuditAction.ApprovalRejected)]
    public async Task Decide_PendingListenerApproval_ResumesTheTaskAndAuditsTheDecision(bool approved, string reply, AuditAction action)
    {
        var conversation = await ConversationAsync();
        var tool = Generated.LowercaseToken(8);
        using (var requestWaiter = factory.Wait(conversation))
        {
            await Queue.EnqueueAsync(DispatchCommand.Message(conversation, $"{IntegrationConstants.ApprovalPrefix} {tool}"), TestContext.Current.CancellationToken);
            await requestWaiter.Settled;
        }

        var pending = Assert.Single(await Approvals.GetPendingAsync(conversation.OperatorId));
        using var decisionWaiter = factory.Wait(conversation);

        var decided = await Approvals.DecideAsync(conversation.OperatorId, pending.Id, approved, Generated.NewMessageText(), TestContext.Current.CancellationToken);

        Assert.True(decided);
        Assert.Equal(RunState.Completed, await decisionWaiter.Settled);
        Assert.Equal($"{reply} {tool}", (await Reader.GetTranscriptAsync(conversation))[^1].Text);
        Assert.Empty(await Approvals.GetPendingAsync(conversation.OperatorId));
        Assert.Contains(factory.ToolListener.Handler.ReceivedDecisions, decision => decision.Operator == conversation.OperatorId && decision.Approved == approved);
        var auditWindow = Generated.NewCountCeiling();
        var audit = await factory.Services.GetRequiredService<IAuditReader>().ReadLatestAsync(auditWindow);
        Assert.Contains(audit, entry => entry.OperatorId == conversation.OperatorId && entry.Action == action && entry.ApprovalId == pending.Id);
        Assert.Contains(audit, entry => entry.OperatorId == conversation.OperatorId && entry.Action == AuditAction.ApprovalRequested && entry.ApprovalId == pending.Id);
    }

    [Fact]
    public async Task Decide_TheSameApprovalTwice_OnlyTheFirstDecisionCounts()
    {
        var conversation = await ConversationAsync();
        using (var requestWaiter = factory.Wait(conversation))
        {
            await Queue.EnqueueAsync(DispatchCommand.Message(conversation, $"{IntegrationConstants.ApprovalPrefix} {Generated.LowercaseToken(8)}"), TestContext.Current.CancellationToken);
            await requestWaiter.Settled;
        }

        var pending = Assert.Single(await Approvals.GetPendingAsync(conversation.OperatorId));
        using var decisionWaiter = factory.Wait(conversation);
        await Approvals.DecideAsync(conversation.OperatorId, pending.Id, true, null, TestContext.Current.CancellationToken);

        var second = await Approvals.DecideAsync(conversation.OperatorId, pending.Id, false, null, TestContext.Current.CancellationToken);

        Assert.False(second);
        await decisionWaiter.Settled;
    }

    [Fact]
    public async Task Cancel_RunningTurn_CancelsTheRemoteTaskAndAuditsIt()
    {
        var conversation = await ConversationAsync();
        using var waiter = factory.Wait(conversation);
        await Queue.EnqueueAsync(DispatchCommand.Message(conversation, $"{IntegrationConstants.SlowPrefix} {Generated.NewMessageText()}"), TestContext.Current.CancellationToken);
        await factory.ToolListener.Handler.SlowTaskStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await waiter.TaskKnown;

        var canceled = Queue.Cancel(conversation);

        Assert.True(canceled);
        Assert.Equal(RunState.Canceled, await waiter.Settled);
        await factory.ToolListener.Handler.SlowTaskCanceled.Task.WaitAsync(TestContext.Current.CancellationToken);
        var auditWindow = Generated.NewCountCeiling();
        var audit = await factory.Services.GetRequiredService<IAuditReader>().ReadLatestAsync(auditWindow);
        Assert.Contains(audit, entry => entry.OperatorId == conversation.OperatorId && entry.Action == AuditAction.RunCanceled);
    }

    [Fact]
    public async Task Message_ToOfflineListener_RecordsThatItWasNotSent()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);
        var conversation = new ConversationKey(Generated.NewUserId(), factory.OfflineListenerId);

        var result = await factory.Services.GetRequiredService<IListenerRunner>().RunAsync(DispatchCommand.Message(conversation, Generated.NewMessageText()), TestContext.Current.CancellationToken);

        Assert.Equal(RunState.Failed, result.State);
        var entry = Assert.Single(await Reader.GetTranscriptAsync(conversation));
        Assert.Equal(TranscriptEntryKind.Error, entry.Kind);
    }

    private async Task<ConversationKey> ConversationAsync()
    {
        await factory.Hosted<ListenerRefreshService>().RefreshAllAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ListenerState.Online, factory.Services.GetRequiredService<IListenerDirectory>().Find(factory.ToolListenerId)?.State);
        return new ConversationKey(Generated.NewUserId(), factory.ToolListenerId);
    }
}
