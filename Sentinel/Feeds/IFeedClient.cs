namespace Sentinel.Feeds;

using Sentinel.Listeners;

public interface IFeedClient
{
    Task<string> GetAsync(ListenerDefinition listener, FeedKind kind, CancellationToken cancellationToken);
}
