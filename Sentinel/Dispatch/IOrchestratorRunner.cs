namespace Sentinel.Dispatch;

public interface IOrchestratorRunner
{
    Task RunAsync(DispatchCommand command, CancellationToken cancellationToken);
}
