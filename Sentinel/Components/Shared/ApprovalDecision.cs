namespace Sentinel.Components.Shared;

public sealed record ApprovalDecision(Guid ApprovalId, bool Approved, string? Reason);
