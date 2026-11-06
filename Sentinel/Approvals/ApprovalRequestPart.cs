namespace Sentinel.Approvals;

using System.Text.Json;
using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ApprovalRequestPart(
    string Kind,
    string ApprovalId,
    string Tool,
    JsonElement? Arguments,
    ApprovalRisk Risk,
    DateTimeOffset? ExpiresAt);
