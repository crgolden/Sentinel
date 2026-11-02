namespace Sentinel.Tests.Unit.Components;

using Bunit;
using Sentinel.Components.Shared;
using Sentinel.Feeds;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class SharedComponentTests : IDisposable
{
    private readonly ComponentHarness _harness = new();

    [Fact]
    public void StatusBadge_RendersTheStateLabelAndClass()
    {
        // Arrange
        var id = Generated.LowercaseToken(8);

        // Act
        var cut = _harness.Context.Render<StatusBadge>(parameters => parameters.Add(p => p.State, ListenerState.Offline).Add(p => p.Id, id));

        // Assert
        var badge = cut.Find($"#{id}");
        Assert.Equal(DisplayMapper.Label(ListenerState.Offline), badge.TextContent);
        Assert.Contains(DisplayMapper.Danger, badge.ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void ListenerTile_OnlineMasterWithStatus_ShowsTheCardMasterBadgeAndHealth()
    {
        // Arrange
        var definition = ListenerFixtures.Definition(ListenerKind.Master);
        var description = Generated.NewMessageText();
        var card = ListenerFixtures.Card(definition.BaseAddress);
        card.Description = description;
        var snapshot = ListenerFixtures.Online(definition) with { Card = card };
        var status = new ListenerStatusFeed { GeneratedAt = Generated.NewUtcTimestamp(), Health = ListenerHealth.Unhealthy, Summary = Generated.NewMessageText() };

        // Act
        var cut = _harness.Context.Render<ListenerTile>(parameters => parameters.Add(p => p.Snapshot, snapshot).Add(p => p.Status, status));

        // Assert
        Assert.Contains(description, cut.Find($"#listener-tile-{snapshot.Id}").TextContent, StringComparison.Ordinal);
        Assert.NotNull(cut.Find($"#listener-master-{snapshot.Id}"));
        Assert.Equal(nameof(ListenerHealth.Unhealthy), cut.Find($"#listener-health-{snapshot.Id}").TextContent);
        Assert.Equal($"listeners/{snapshot.Id}", cut.Find($"#listener-open-{snapshot.Id}").GetAttribute("href"));
    }

    [Fact]
    public void ListenerTile_Misconfigured_ShowsTheProblem()
    {
        // Arrange
        var snapshot = new ListenerSnapshot(ListenerFixtures.Definition(), ListenerState.Misconfigured, ListenerProblem.ForeignOrigin, null, Generated.NewUtcTimestamp(), false);

        // Act
        var cut = _harness.Context.Render<ListenerTile>(parameters => parameters.Add(p => p.Snapshot, snapshot));

        // Assert
        Assert.Equal(DisplayMapper.Label(ListenerProblem.ForeignOrigin), cut.Find($"#listener-problem-{snapshot.Id}").TextContent);
    }

    [Fact]
    public void ListenerTile_NoSnapshot_RendersNothing()
    {
        // Act
        var cut = _harness.Context.Render<ListenerTile>();

        // Assert
        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void ArtifactList_Items_RendersEachWithItsContentAndLink()
    {
        // Arrange
        var text = Generated.NewMessageText();
        var link = Generated.NewHttpsUri(Generated.NewHostname());
        var item = new ArtifactItem(Generated.LowercaseToken(8), Generated.NewDisplayName(), ArtifactKind.Report, Generated.NewUtcTimestamp(), Generated.NewMessageText(), link, text);
        var id = Generated.LowercaseToken(8);

        // Act
        var cut = _harness.Context.Render<ArtifactList>(parameters => parameters.Add(p => p.Items, [item]).Add(p => p.Id, id));

        // Assert
        var rendered = cut.Find($"#artifact-{item.Id}");
        Assert.Contains(item.Title, rendered.TextContent, StringComparison.Ordinal);
        Assert.Contains(text, rendered.TextContent, StringComparison.Ordinal);
        Assert.Equal(link.ToString(), rendered.QuerySelector("a")?.GetAttribute("href"));
    }

    [Fact]
    public void ArtifactList_NoItems_SaysSo()
    {
        // Arrange
        var id = Generated.LowercaseToken(8);

        // Act
        var cut = _harness.Context.Render<ArtifactList>(parameters => parameters.Add(p => p.Items, []).Add(p => p.Id, id));

        // Assert
        Assert.Empty(cut.FindAll("li"));
    }

    [Fact]
    public void ApprovalCard_Approve_RaisesTheCallbackWithTheReason()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending();
        var reason = Generated.NewMessageText();
        List<ApprovalDecision> decisions = [];
        var cut = _harness.Context.Render<ApprovalCard>(parameters => parameters
            .Add(p => p.Approval, approval)
            .Add(p => p.OnDecided, decision => decisions.Add(decision)));
        cut.Find($"#approval-reason-{approval.Id:N}").Change(reason);

        // Act
        cut.Find($"#approve-{approval.Id:N}").Click();

        // Assert
        Assert.Equal([new ApprovalDecision(approval.Id, true, reason)], decisions);
    }

    [Fact]
    public void ApprovalCard_Reject_RaisesTheCallbackWithTheReason()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending();
        var reason = Generated.NewMessageText();
        List<ApprovalDecision> decisions = [];
        var cut = _harness.Context.Render<ApprovalCard>(parameters => parameters
            .Add(p => p.Approval, approval)
            .Add(p => p.OnDecided, decision => decisions.Add(decision)));
        cut.Find($"#approval-reason-{approval.Id:N}").Change(reason);

        // Act
        cut.Find($"#reject-{approval.Id:N}").Click();

        // Assert
        Assert.Equal([new ApprovalDecision(approval.Id, false, reason)], decisions);
    }

    [Fact]
    public void ApprovalCard_ShowsTheToolArgumentsAndRisk()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending();

        // Act
        var cut = _harness.Context.Render<ApprovalCard>(parameters => parameters.Add(p => p.Approval, approval).Add(p => p.OnDecided, _ => { }));

        // Assert
        Assert.Equal(approval.ArgumentsJson, cut.Find($"#approval-arguments-{approval.Id:N}").TextContent);
        Assert.Equal(approval.Risk.ToString(), cut.Find($"#approval-risk-{approval.Id:N}").TextContent);
    }

    public void Dispose() => _harness.Dispose();
}
