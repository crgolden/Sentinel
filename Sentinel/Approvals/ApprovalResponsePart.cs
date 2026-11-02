namespace Sentinel.Approvals;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ApprovalResponsePart(string Kind, string ApprovalId, bool Approved, string? Reason, Guid Operator);
