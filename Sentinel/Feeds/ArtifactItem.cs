namespace Sentinel.Feeds;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ArtifactItem(
    string Id,
    string Title,
    ArtifactKind Kind,
    DateTimeOffset ProducedAt,
    string? Summary,
    Uri? Link,
    string? Text);
