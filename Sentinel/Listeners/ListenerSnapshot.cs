namespace Sentinel.Listeners;

using A2A;

public sealed record ListenerSnapshot(
    ListenerDefinition Definition,
    ListenerState State,
    ListenerProblem Problem,
    AgentCard? Card,
    DateTimeOffset? LastCheckedAt,
    bool FeedsSupported)
{
    public string Id => Definition.Id;

    public static ListenerSnapshot Unchecked(ListenerDefinition definition) =>
        new(definition, ListenerState.Unknown, ListenerProblem.None, null, null, false);
}
