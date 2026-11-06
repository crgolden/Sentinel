namespace Sentinel.Conversations;

using A2A;

public static class SessionPolicy
{
    public static bool NeedsFreshSession(TaskState? taskState) =>
        taskState is TaskState.Completed or TaskState.Failed or TaskState.Canceled or TaskState.Rejected;

    public static RunState Outcome(TaskState? taskState, bool approvalRequested) => taskState switch
    {
        TaskState.InputRequired when approvalRequested => RunState.AwaitingApproval,
        TaskState.Failed or TaskState.Rejected or TaskState.AuthRequired => RunState.Failed,
        TaskState.Canceled => RunState.Canceled,
        TaskState.Submitted or TaskState.Working => RunState.Interrupted,
        _ => RunState.Completed,
    };
}
