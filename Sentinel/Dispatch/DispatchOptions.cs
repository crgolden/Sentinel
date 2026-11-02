namespace Sentinel.Dispatch;

public sealed record DispatchOptions(int QueueCapacity, int MaxConcurrentRuns, int CancelTimeoutSeconds);
