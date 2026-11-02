namespace Sentinel.Feeds;

public interface IFeedCache : IFeedReader
{
    Task StoreAsync(string listenerId, FeedKind kind, string json);
}
