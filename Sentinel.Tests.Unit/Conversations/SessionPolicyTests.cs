namespace Sentinel.Tests.Unit.Conversations;

using A2A;
using Sentinel.Conversations;

[Trait("Category", "Unit")]
public sealed class SessionPolicyTests
{
    [Theory]
    [InlineData(TaskState.Completed, true)]
    [InlineData(TaskState.Failed, true)]
    [InlineData(TaskState.Canceled, true)]
    [InlineData(TaskState.Rejected, true)]
    [InlineData(TaskState.InputRequired, false)]
    [InlineData(TaskState.Working, false)]
    [InlineData(TaskState.Submitted, false)]
    [InlineData(TaskState.AuthRequired, false)]
    public void NeedsFreshSession_TerminalStatesOnly(TaskState state, bool expected)
    {
        // Act
        var actual = SessionPolicy.NeedsFreshSession(state);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NeedsFreshSession_NoTaskYet_ReturnsFalse()
    {
        // Act
        var actual = SessionPolicy.NeedsFreshSession(null);

        // Assert
        Assert.False(actual);
    }

    [Theory]
    [InlineData(TaskState.InputRequired, true, RunState.AwaitingApproval)]
    [InlineData(TaskState.InputRequired, false, RunState.Completed)]
    [InlineData(TaskState.Completed, false, RunState.Completed)]
    [InlineData(TaskState.Failed, false, RunState.Failed)]
    [InlineData(TaskState.Rejected, false, RunState.Failed)]
    [InlineData(TaskState.AuthRequired, false, RunState.Failed)]
    [InlineData(TaskState.Canceled, false, RunState.Canceled)]
    [InlineData(TaskState.Working, false, RunState.Interrupted)]
    [InlineData(TaskState.Submitted, false, RunState.Interrupted)]
    public void Outcome_MapsTheFinalTaskState(TaskState state, bool approvalRequested, RunState expected)
    {
        // Act
        var actual = SessionPolicy.Outcome(state, approvalRequested);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Outcome_PlainMessageReply_IsCompleted()
    {
        // Act
        var actual = SessionPolicy.Outcome(null, false);

        // Assert
        Assert.Equal(RunState.Completed, actual);
    }
}
