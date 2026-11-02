namespace Sentinel.Feeds;

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class BoardSnapshot
{
    public required DateTimeOffset GeneratedAt { get; init; }

    [AllowNull]
    public IReadOnlyList<SessionRow> Sessions { get => field; init => field = value ?? []; } = [];

    [AllowNull]
    public IReadOnlyList<ClaimRow> Claims { get => field; init => field = value ?? []; } = [];

    [AllowNull]
    public IReadOnlyList<GateRow> Gates { get => field; init => field = value ?? []; } = [];

    [AllowNull]
    public IReadOnlyList<HandoffRow> Handoffs { get => field; init => field = value ?? []; } = [];

    [AllowNull]
    public IReadOnlyList<AuditRow> Audits { get => field; init => field = value ?? []; } = [];

    [AllowNull]
    public IReadOnlyList<InventoryRow> Inventory { get => field; init => field = value ?? []; } = [];
}
