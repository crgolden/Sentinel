namespace Sentinel.Listeners;

public interface IListenerDirectory
{
    event Action? Changed;

    IReadOnlyList<ListenerSnapshot> Listeners { get; }

    ListenerSnapshot? Find(string listenerId);
}
