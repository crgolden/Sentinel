namespace Sentinel.Listeners;

public sealed record ListenerDefinition(string Id, string DisplayName, Uri BaseAddress, ListenerKind Kind, bool Pinned);
