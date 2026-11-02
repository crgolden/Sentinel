namespace Sentinel.Feeds;

public sealed record FeedRefreshOptions(int IntervalSeconds, int MaxFeedBytes);
