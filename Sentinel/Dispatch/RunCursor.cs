namespace Sentinel.Dispatch;

using Sentinel.Conversations;

internal sealed class RunCursor(RunRecord run)
{
    public RunRecord Run { get; set; } = run;
}
