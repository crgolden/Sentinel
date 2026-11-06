namespace Sentinel.Approvals;

public sealed record OrchestratorToolCall(string RequestId, string CallId, string Name, string ArgumentsJson);
