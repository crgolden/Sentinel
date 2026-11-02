namespace Sentinel.Health;

public sealed record KeepaliveOptions(Uri PingUri, TimeSpan Interval);
