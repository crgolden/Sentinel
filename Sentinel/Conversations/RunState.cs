namespace Sentinel.Conversations;

public enum RunState
{
    Running,
    AwaitingApproval,
    Completed,
    Failed,
    Interrupted,
    Canceled,
}
