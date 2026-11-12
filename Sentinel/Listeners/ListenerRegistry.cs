namespace Sentinel.Listeners;

public sealed class ListenerRegistry : IListenerDirectory
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ListenerSnapshot> _snapshots;
    private IReadOnlyList<ListenerSnapshot> _ordered;

    public ListenerRegistry(IReadOnlyList<ListenerDefinition> definitions)
    {
        _snapshots = ListenerCatalog.Validate(definitions)
            .ToDictionary(definition => definition.Id, ListenerSnapshot.Unchecked, StringComparer.Ordinal);
        _ordered = Order(_snapshots.Values);
    }

    public event Action? Changed;

    public IReadOnlyList<ListenerSnapshot> Listeners => _ordered;

    public IReadOnlyList<ListenerDefinition> Definitions => [.. _ordered.Select(snapshot => snapshot.Definition)];

    public ListenerSnapshot? Find(string listenerId)
    {
        lock (_lock)
        {
            return _snapshots.GetValueOrDefault(listenerId);
        }
    }

    public void Update(ListenerSnapshot snapshot)
    {
        bool changed;
        lock (_lock)
        {
            if (!_snapshots.TryGetValue(snapshot.Id, out var previous))
            {
                throw new InvalidOperationException($"Unknown listener '{snapshot.Id}'.");
            }

            changed = previous.State != snapshot.State
                || previous.Problem != snapshot.Problem
                || previous.FeedsSupported != snapshot.FeedsSupported
                || !string.Equals(previous.Card?.Version, snapshot.Card?.Version, StringComparison.Ordinal);
            _snapshots[snapshot.Id] = snapshot;
            _ordered = Order(_snapshots.Values);
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private static IReadOnlyList<ListenerSnapshot> Order(IEnumerable<ListenerSnapshot> snapshots) =>
    [
        .. snapshots
            .OrderByDescending(snapshot => snapshot.Definition.Kind == ListenerKind.Master, Comparer<bool>.Default)
            .ThenByDescending(snapshot => snapshot.Definition.Pinned, Comparer<bool>.Default)
            .ThenBy(snapshot => snapshot.Definition.DisplayName, StringComparer.Ordinal),
    ];
}
