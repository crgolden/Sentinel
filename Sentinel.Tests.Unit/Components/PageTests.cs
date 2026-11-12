namespace Sentinel.Tests.Unit.Components;

using System.Globalization;
using Bunit;
using Moq;
using Sentinel.Approvals;
using Sentinel.Components.Pages;
using Sentinel.Components.Shared;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Feeds;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class PageTests : IDisposable
{
    private readonly ComponentHarness _harness = new();

    [Fact]
    public void Dashboard_Listeners_RendersATileEachAndThePendingCount()
    {
        // Arrange
        var tool = ListenerFixtures.Online(ListenerFixtures.Definition());
        var master = ListenerFixtures.Offline(ListenerFixtures.Definition(ListenerKind.Master));
        _harness.Listeners(tool, master);
        _harness.NoFeeds(tool.Id);
        _harness.NoFeeds(master.Id);
        PendingApproval[] pending = [ApprovalFixtures.Pending()];
        _harness.Approvals.Setup(approvals => approvals.GetPendingAsync(_harness.OperatorId)).ReturnsAsync(pending);

        // Act
        var cut = _harness.Context.Render<Dashboard>();

        // Assert
        Assert.NotNull(cut.Find($"#{DisplayMapper.ElementId(ElementIds.ListenerTile, tool.Id)}"));
        Assert.NotNull(cut.Find($"#{DisplayMapper.ElementId(ElementIds.ListenerTile, master.Id)}"));
        Assert.Equal(pending.Length.ToString(CultureInfo.InvariantCulture), cut.Find("#dashboard-approval-count").TextContent);
    }

    [Fact]
    public void Dashboard_NoListeners_SaysSo()
    {
        // Arrange
        _harness.Listeners();
        _harness.Approvals.Setup(approvals => approvals.GetPendingAsync(_harness.OperatorId)).ReturnsAsync([]);

        // Act
        var cut = _harness.Context.Render<Dashboard>();

        // Assert
        Assert.NotNull(cut.Find("#dashboard-empty"));
    }

    [Fact]
    public void ListenerPage_KnownListenerWithFeeds_ShowsStatusArtifactsAndChat()
    {
        // Arrange
        var listener = ListenerFixtures.Online(ListenerFixtures.Definition());
        var check = new StatusCheck(Generated.NewDisplayName(), ListenerHealth.Healthy, Generated.NewMessageText(), Generated.NewUtcTimestamp());
        var artifact = new ArtifactItem(Generated.LowercaseToken(8), Generated.NewDisplayName(), ArtifactKind.Log, Generated.NewUtcTimestamp(), null, null, null);
        _harness.Listeners(listener);
        _harness.Feeds.Setup(feeds => feeds.GetStatusAsync(listener.Id)).ReturnsAsync(new ListenerStatusFeed { GeneratedAt = Generated.NewUtcTimestamp(), Health = ListenerHealth.Healthy, Checks = [check] });
        _harness.Feeds.Setup(feeds => feeds.GetArtifactsAsync(listener.Id)).ReturnsAsync(new ArtifactFeed { GeneratedAt = Generated.NewUtcTimestamp(), Items = [artifact] });
        _harness.EmptyConversation(new ConversationKey(_harness.OperatorId, listener.Id));

        // Act
        var cut = _harness.Context.Render<ListenerPage>(parameters => parameters.Add(p => p.ListenerId, listener.Id));

        // Assert
        Assert.Equal(listener.Definition.DisplayName, cut.Find("#listener-title").TextContent);
        Assert.Contains(check.Name, cut.Find("#listener-status").TextContent, StringComparison.Ordinal);
        Assert.NotNull(cut.Find($"#{DisplayMapper.ElementId(ElementIds.Artifact, artifact.Id)}"));
        Assert.NotNull(cut.Find("#chat-panel"));
    }

    [Fact]
    public void ListenerPage_NoStatusFeed_SaysSo()
    {
        // Arrange
        var listener = ListenerFixtures.Online(ListenerFixtures.Definition(ListenerKind.Master));
        _harness.Listeners(listener);
        _harness.NoFeeds(listener.Id);
        _harness.EmptyConversation(new ConversationKey(_harness.OperatorId, listener.Id));

        // Act
        var cut = _harness.Context.Render<ListenerPage>(parameters => parameters.Add(p => p.ListenerId, listener.Id));

        // Assert
        Assert.NotNull(cut.Find("#listener-status-none"));
        Assert.NotNull(cut.Find("#listener-page-master"));
    }

    [Fact]
    public void ListenerPage_UnknownListener_SaysSo()
    {
        // Arrange
        var listenerId = Generated.LowercaseToken(8);
        _harness.Listeners();
        _harness.Directory.Setup(directory => directory.Find(listenerId)).Returns((ListenerSnapshot?)null);
        _harness.NoFeeds(listenerId);

        // Act
        var cut = _harness.Context.Render<ListenerPage>(parameters => parameters.Add(p => p.ListenerId, listenerId));

        // Assert
        Assert.NotNull(cut.Find("#listener-missing"));
    }

    [Fact]
    public void OrchestratorChat_OpensTheOperatorsOrchestratorConversation()
    {
        // Arrange
        _harness.EmptyConversation(ConversationKey.Orchestrator(_harness.OperatorId));

        // Act
        var cut = _harness.Context.Render<OrchestratorChat>();

        // Assert
        Assert.NotNull(cut.Find("#chat-panel"));
        _harness.Reader.Verify(reader => reader.GetTranscriptAsync(ConversationKey.Orchestrator(_harness.OperatorId)), Times.Once);
    }

    [Fact]
    public void ApprovalsPage_NothingPending_SaysSo()
    {
        // Arrange
        _harness.Approvals.Setup(approvals => approvals.GetPendingAsync(_harness.OperatorId)).ReturnsAsync([]);

        // Act
        var cut = _harness.Context.Render<ApprovalsPage>();

        // Assert
        Assert.NotNull(cut.Find("#approvals-empty"));
    }

    [Fact]
    public void ApprovalsPage_Approve_DecidesAndReloads()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending();
        _harness.Approvals.SetupSequence(approvals => approvals.GetPendingAsync(_harness.OperatorId)).ReturnsAsync([approval]).ReturnsAsync([]);
        _harness.Approvals.Setup(approvals => approvals.DecideAsync(_harness.OperatorId, approval.Id, true, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var cut = _harness.Context.Render<ApprovalsPage>();

        // Act
        cut.Find($"#{DisplayMapper.ElementId(ElementIds.Approve, approval.Id)}").Click();

        // Assert
        Assert.NotNull(cut.Find("#approvals-message"));
        Assert.NotNull(cut.Find("#approvals-empty"));
    }

    [Fact]
    public void ApprovalsPage_ApprovalRequestedEvent_Reloads()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending(conversation: new ConversationKey(_harness.OperatorId, Generated.LowercaseToken(8)));
        _harness.Approvals.SetupSequence(approvals => approvals.GetPendingAsync(_harness.OperatorId)).ReturnsAsync([]).ReturnsAsync([approval]);
        var cut = _harness.Context.Render<ApprovalsPage>();
        var entry = Transcripts.Entry(TranscriptRole.Agent, TranscriptEntryKind.ApprovalRequested);

        // Act
        _harness.Hub.Publish(new ConversationEvent(approval.Conversation, ConversationEventKind.EntryAppended, entry, null, null));

        // Assert
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"#{DisplayMapper.ElementId(ElementIds.ApprovalCard, approval.Id)}")));
    }

    [Fact]
    public void BoardPage_MasterBoard_RendersEveryCollection()
    {
        // Arrange
        var master = ListenerFixtures.Online(ListenerFixtures.Definition(ListenerKind.Master));
        var sessionId = Guid.NewGuid().ToString();
        var session = new SessionRow(sessionId, Generated.NewHostLabel(), Generated.NewMessageText(), null, Generated.NewUtcTimestamp());
        var handoff = new HandoffRow(Generated.LowercaseToken(10), Generated.LowercaseToken(6), Generated.LowercaseToken(6), Generated.NewMessageText(), Generated.LowercaseToken(5), null, Generated.NewUtcTimestamp());
        var claim = new ClaimRow { SessionId = session.SessionId, Repo = Generated.LowercaseToken(6), ClaimedAt = Generated.NewUtcTimestamp(), Files = [Generated.LowercaseToken(6)] };
        var gate = new GateRow(session.SessionId, session.Machine, Generated.LowercaseToken(6), GateState.Finished, Generated.LowercaseToken(6), Generated.NewUtcTimestamp());
        var inventory = new InventoryRow(session.Machine, session.SessionId, Generated.LowercaseToken(6), session.Goal, Generated.NewUtcTimestamp());
        _harness.Listeners(master);
        _harness.Feeds.Setup(feeds => feeds.GetBoardAsync(master.Id)).ReturnsAsync(new BoardSnapshot
        {
            GeneratedAt = Generated.NewUtcTimestamp(),
            Sessions = [session],
            Claims = [claim],
            Gates = [gate],
            Handoffs = [handoff],
            Inventory = [inventory],
        });

        // Act
        var cut = _harness.Context.Render<BoardPage>();

        // Assert
        Assert.Contains(session.SessionId, cut.Find("#board-sessions").TextContent, StringComparison.Ordinal);
        Assert.Contains(handoff.Summary, cut.Find("#board-handoffs").TextContent, StringComparison.Ordinal);
        Assert.Collection(cut.FindAll(RowsOf(ElementIds.BoardClaimRow)), row => Assert.Contains(claim.Repo, row.TextContent, StringComparison.Ordinal));
        Assert.Collection(cut.FindAll(RowsOf(ElementIds.BoardGateRow)), row => Assert.Contains(gate.Repo, row.TextContent, StringComparison.Ordinal));
        Assert.Collection(cut.FindAll(RowsOf(ElementIds.BoardInventoryRow)), row => Assert.Contains(inventory.State, row.TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void BoardPage_NoMaster_SaysSo()
    {
        // Arrange
        _harness.Listeners(ListenerFixtures.Online(ListenerFixtures.Definition()));

        // Act
        var cut = _harness.Context.Render<BoardPage>();

        // Assert
        Assert.NotNull(cut.Find("#board-none"));
    }

    [Fact]
    public void AuditsPage_RunsAndReports_RendersBoth()
    {
        // Arrange
        var master = ListenerFixtures.Online(ListenerFixtures.Definition(ListenerKind.Master));
        var run = new AuditRow(Generated.LowercaseToken(8), Generated.NewUtcTimestamp(), Generated.NewMessageText(), Generated.NewHttpsUri(Generated.NewHostname()), Generated.NewUtcTimestamp());
        var report = new ArtifactItem(Generated.LowercaseToken(8), Generated.NewDisplayName(), ArtifactKind.AuditReport, Generated.NewUtcTimestamp(), null, null, null);
        var log = new ArtifactItem(Generated.LowercaseToken(8), Generated.NewDisplayName(), ArtifactKind.Log, Generated.NewUtcTimestamp(), null, null, null);
        _harness.Listeners(master);
        _harness.Feeds.Setup(feeds => feeds.GetBoardAsync(master.Id)).ReturnsAsync(new BoardSnapshot { GeneratedAt = Generated.NewUtcTimestamp(), Audits = [run] });
        _harness.Feeds.Setup(feeds => feeds.GetArtifactsAsync(master.Id)).ReturnsAsync(new ArtifactFeed { GeneratedAt = Generated.NewUtcTimestamp(), Items = [report, log] });

        // Act
        var cut = _harness.Context.Render<AuditsPage>();

        // Assert
        Assert.Contains(run.Summary, cut.Find("#audit-runs").TextContent, StringComparison.Ordinal);
        Assert.NotNull(cut.Find($"#{DisplayMapper.ElementId(ElementIds.Artifact, report.Id)}"));
        Assert.Empty(cut.FindAll($"#{DisplayMapper.ElementId(ElementIds.Artifact, log.Id)}"));
    }

    [Fact]
    public void AuditsPage_NothingRelayed_SaysSo()
    {
        // Arrange
        _harness.Listeners();

        // Act
        var cut = _harness.Context.Render<AuditsPage>();

        // Assert
        Assert.NotNull(cut.Find("#audits-none"));
    }

    [Fact]
    public void ArtifactsPage_GroupsArtifactsByListener()
    {
        // Arrange
        var withArtifacts = ListenerFixtures.Online(ListenerFixtures.Definition());
        var without = ListenerFixtures.Online(ListenerFixtures.Definition());
        var item = new ArtifactItem(Generated.LowercaseToken(8), Generated.NewDisplayName(), ArtifactKind.Snapshot, Generated.NewUtcTimestamp(), null, null, null);
        _harness.Listeners(withArtifacts, without);
        _harness.Feeds.Setup(feeds => feeds.GetArtifactsAsync(withArtifacts.Id)).ReturnsAsync(new ArtifactFeed { GeneratedAt = Generated.NewUtcTimestamp(), Items = [item] });
        _harness.Feeds.Setup(feeds => feeds.GetArtifactsAsync(without.Id)).ReturnsAsync((ArtifactFeed?)null);

        // Act
        var cut = _harness.Context.Render<ArtifactsPage>();

        // Assert
        Assert.NotNull(cut.Find($"#{DisplayMapper.ElementId(ElementIds.ListenerArtifacts, withArtifacts.Id)}"));
        Assert.Empty(cut.FindAll($"#{DisplayMapper.ElementId(ElementIds.ListenerArtifacts, without.Id)}"));
    }

    [Fact]
    public void ArtifactsPage_NoArtifacts_SaysSo()
    {
        // Arrange
        _harness.Listeners();

        // Act
        var cut = _harness.Context.Render<ArtifactsPage>();

        // Assert
        Assert.NotNull(cut.Find("#artifacts-none"));
    }

    [Fact]
    public void AuditLogPage_Entries_RendersARowEach()
    {
        // Arrange
        var approvalId = Guid.NewGuid();
        var granted = new AuditEntry(Generated.NewUtcTimestamp(), _harness.OperatorId, AuditAction.ApprovalGranted, Generated.LowercaseToken(8), approvalId, Generated.NewMessageText());
        var dispatched = new AuditEntry(Generated.NewUtcTimestamp(), _harness.OperatorId, AuditAction.MessageDispatched, Generated.LowercaseToken(8), null, null);
        _harness.Audit.Setup(audit => audit.ReadLatestAsync(It.IsAny<int>())).ReturnsAsync([granted, dispatched]);

        // Act
        var cut = _harness.Context.Render<AuditLogPage>();

        // Assert
        Assert.Collection(
            cut.FindAll(RowsOf(ElementIds.AuditLogRow)),
            row => Assert.Contains(granted.TargetId, row.TextContent, StringComparison.Ordinal),
            row => Assert.Contains(dispatched.TargetId, row.TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void AuditLogPage_NoEntries_SaysSo()
    {
        // Arrange
        _harness.Audit.Setup(audit => audit.ReadLatestAsync(It.IsAny<int>())).ReturnsAsync([]);

        // Act
        var cut = _harness.Context.Render<AuditLogPage>();

        // Assert
        Assert.NotNull(cut.Find("#audit-log-empty"));
    }

    [Fact]
    public void ErrorPage_Renders()
    {
        // Act
        var cut = _harness.Context.Render<Error>();

        // Assert
        Assert.NotNull(cut.Find("#error-page"));
    }

    public void Dispose() => _harness.Dispose();

    private static string RowsOf(string rowPrefix) => $"[id^='{rowPrefix}-']";
}
