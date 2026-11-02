namespace Sentinel.Components.Pages;

using Sentinel.Feeds;

public sealed record ArtifactGroup(string ListenerId, string DisplayName, IReadOnlyList<ArtifactItem> Items);
