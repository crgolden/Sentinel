namespace Sentinel.Tests.Unit.Components;

using Sentinel.Approvals;
using Sentinel.Components.Shared;
using Sentinel.Conversations;
using Sentinel.Feeds;
using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class DisplayMapperTests
{
    [Theory]
    [InlineData(ListenerState.Online, DisplayMapper.Success)]
    [InlineData(ListenerState.Offline, DisplayMapper.Danger)]
    [InlineData(ListenerState.Misconfigured, DisplayMapper.Warning)]
    [InlineData(ListenerState.Unknown, DisplayMapper.Neutral)]
    public void ListenerState_MapsToBadge(ListenerState state, string badge)
    {
        // Act
        var actual = DisplayMapper.BadgeClass(state);

        // Assert
        Assert.Equal(badge, actual);
    }

    [Theory]
    [InlineData(ListenerHealth.Healthy, DisplayMapper.Success)]
    [InlineData(ListenerHealth.Degraded, DisplayMapper.Warning)]
    [InlineData(ListenerHealth.Unhealthy, DisplayMapper.Danger)]
    public void ListenerHealth_MapsToBadge(ListenerHealth health, string badge)
    {
        // Act
        var actual = DisplayMapper.BadgeClass(health);

        // Assert
        Assert.Equal(badge, actual);
    }

    [Theory]
    [InlineData(RunState.Running, DisplayMapper.Info)]
    [InlineData(RunState.AwaitingApproval, DisplayMapper.Warning)]
    [InlineData(RunState.Completed, DisplayMapper.Success)]
    [InlineData(RunState.Failed, DisplayMapper.Danger)]
    [InlineData(RunState.Interrupted, DisplayMapper.Neutral)]
    [InlineData(RunState.Canceled, DisplayMapper.Neutral)]
    public void RunState_MapsToBadge(RunState state, string badge)
    {
        // Act
        var actual = DisplayMapper.BadgeClass(state);

        // Assert
        Assert.Equal(badge, actual);
    }

    [Theory]
    [InlineData(ApprovalRisk.High, DisplayMapper.Danger)]
    [InlineData(ApprovalRisk.Low, DisplayMapper.Warning)]
    public void ApprovalRisk_MapsToBadge(ApprovalRisk risk, string badge)
    {
        // Act
        var actual = DisplayMapper.BadgeClass(risk);

        // Assert
        Assert.Equal(badge, actual);
    }

    [Theory]
    [InlineData(ListenerState.Online, "border-success")]
    [InlineData(ListenerState.Offline, "border-danger")]
    [InlineData(ListenerState.Misconfigured, "border-warning")]
    [InlineData(ListenerState.Unknown, "border-secondary")]
    public void BorderClass_ToolListener_FollowsTheState(ListenerState state, string border)
    {
        // Arrange
        var snapshot = ListenerFixtures.Online(ListenerFixtures.Definition()) with { State = state };

        // Act
        var actual = DisplayMapper.BorderClass(snapshot);

        // Assert
        Assert.Equal(border, actual);
    }

    [Fact]
    public void BorderClass_MasterBridge_IsPrimary()
    {
        // Arrange
        var snapshot = ListenerFixtures.Offline(ListenerFixtures.Definition(ListenerKind.Master));

        // Act
        var actual = DisplayMapper.BorderClass(snapshot);

        // Assert
        Assert.Equal("border-primary", actual);
    }

    [Theory]
    [InlineData(TranscriptRole.Agent, TranscriptEntryKind.Error, "list-group-item-danger")]
    [InlineData(TranscriptRole.Agent, TranscriptEntryKind.ApprovalRequested, "list-group-item-warning")]
    [InlineData(TranscriptRole.Operator, TranscriptEntryKind.ApprovalDecided, "list-group-item-info")]
    [InlineData(TranscriptRole.System, TranscriptEntryKind.Status, "list-group-item-light")]
    [InlineData(TranscriptRole.Operator, TranscriptEntryKind.Message, "list-group-item-primary")]
    [InlineData(TranscriptRole.Agent, TranscriptEntryKind.Message, "list-group-item")]
    public void EntryClass_FollowsKindThenRole(TranscriptRole role, TranscriptEntryKind kind, string expected)
    {
        // Act
        var actual = DisplayMapper.EntryClass(Transcripts.Entry(role, kind));

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Label_EveryStateAndRoleIsDistinct()
    {
        // Act
        string[][] labels =
        [
            [.. Enum.GetValues<ListenerState>().Select(DisplayMapper.Label)],
            [.. Enum.GetValues<RunState>().Select(DisplayMapper.Label)],
            [.. Enum.GetValues<TranscriptRole>().Select(DisplayMapper.Label)],
        ];

        // Assert
        Assert.All(labels, group => Assert.Equal(group.Length, group.Distinct(StringComparer.Ordinal).Count()));
    }

    [Theory]
    [InlineData(ListenerProblem.None)]
    [InlineData(ListenerProblem.Unreachable)]
    [InlineData(ListenerProblem.NoInterfaces)]
    [InlineData(ListenerProblem.InsecureInterface)]
    [InlineData(ListenerProblem.ForeignOrigin)]
    public void Label_EveryProblemHasText(ListenerProblem problem)
    {
        // Act
        var actual = DisplayMapper.Label(problem);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(actual));
    }

    [Fact]
    public void Timestamp_FormatsInUtc()
    {
        // Arrange
        var at = Generated.NewTimestampWithNonZeroOffset();

        // Act
        var actual = DisplayMapper.Timestamp(at);

        // Assert
        Assert.Equal(at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture), actual);
    }

    [Fact]
    public void Timestamp_Missing_IsNever()
    {
        // Act
        var actual = DisplayMapper.Timestamp(null);

        // Assert
        Assert.Equal("Never", actual);
    }

    [Fact]
    public void ElementId_JoinsThePrefixAndId()
    {
        // Arrange
        var prefix = Generated.LowercaseToken(6);
        var id = Guid.NewGuid();

        // Act
        var actual = (DisplayMapper.ElementId(prefix, id), DisplayMapper.ElementId(prefix, prefix));

        // Assert
        Assert.Equal(($"{prefix}-{id:N}", $"{prefix}-{prefix}"), actual);
    }
}
