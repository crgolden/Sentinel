namespace Sentinel.Dispatch;

public interface IListenerRunner
{
    Task<ListenerTurnResult> RunAsync(DispatchCommand command, CancellationToken cancellationToken);
}
