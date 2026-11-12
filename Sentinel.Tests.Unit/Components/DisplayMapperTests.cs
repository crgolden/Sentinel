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
    [InlineData(ListenerState.Online, DisplayMapper.BorderSuccess)]
    [InlineData(ListenerState.Offline, DisplayMapper.BorderDanger)]
    [InlineData(ListenerState.Misconfigured, DisplayMapper.BorderWarning)]
    [InlineData(ListenerState.Unknown, DisplayMapper.BorderNeutral)]
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
        Assert.Equal(DisplayMapper.BorderPrimary, actual);
    }

    [Theory]
    [InlineData(TranscriptRole.Agent, TranscriptEntryKind.Error, DisplayMapper.EntryDanger)]
    [InlineData(TranscriptRole.Agent, TranscriptEntryKind.ApprovalRequested, DisplayMapper.EntryWarning)]
    [InlineData(TranscriptRole.Operator, TranscriptEntryKind.ApprovalDecided, DisplayMapper.EntryInfo)]
    [InlineData(TranscriptRole.System, TranscriptEntryKind.Status, DisplayMapper.EntryLight)]
    [InlineData(TranscriptRole.Operator, TranscriptEntryKind.Message, DisplayMapper.EntryPrimary)]
    [InlineData(TranscriptRole.Agent, TranscriptEntryKind.Message, DisplayMapper.EntryDefault)]
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
        Assert.Equal(at.ToUniversalTime().ToString(DisplayMapper.TimestampFormat, System.Globalization.CultureInfo.InvariantCulture), actual);
    }

    [Fact]
    public void Timestamp_Missing_IsNever()
    {
        // Act
        var actual = DisplayMapper.Timestamp(null);

        // Assert
        Assert.Equal(DisplayMapper.NeverTimestamp, actual);
    }

    [Fact]
    public void ElementId_JoinsThePrefixAndId()
    {
        // Arrange
        var prefix = Generated.LowercaseToken(6);
        var entityId = Guid.NewGuid();

        // Act
        var actual = (DisplayMapper.ElementId(prefix, entityId), DisplayMapper.ElementId(prefix, prefix));

        // Assert
        Assert.Equal(($"{prefix}-{entityId:N}", $"{prefix}-{prefix}"), actual);
    }
}
