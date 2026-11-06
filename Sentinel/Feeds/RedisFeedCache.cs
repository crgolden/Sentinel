namespace Sentinel.Feeds;

using System.Text.Json;
using Sentinel.Conversations;
using StackExchange.Redis;

public sealed class RedisFeedCache(IDatabase database) : IFeedCache
{
    public async Task StoreAsync(string listenerId, FeedKind kind, string json)
    {
        using var document = JsonDocument.Parse(json);
        var validated = kind switch
        {
            FeedKind.Status => JsonSerializer.Serialize(document.Deserialize<ListenerStatusFeed>(SentinelJson.Options), SentinelJson.Options),
            FeedKind.Artifacts => JsonSerializer.Serialize(document.Deserialize<ArtifactFeed>(SentinelJson.Options), SentinelJson.Options),
            FeedKind.Board => JsonSerializer.Serialize(document.Deserialize<BoardSnapshot>(SentinelJson.Options), SentinelJson.Options),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        await database.StringSetAsync(RedisKeys.Feed(listenerId, kind), validated);
    }

    public Task<ListenerStatusFeed?> GetStatusAsync(string listenerId) => ReadAsync<ListenerStatusFeed>(listenerId, FeedKind.Status);

    public Task<ArtifactFeed?> GetArtifactsAsync(string listenerId) => ReadAsync<ArtifactFeed>(listenerId, FeedKind.Artifacts);

    public Task<BoardSnapshot?> GetBoardAsync(string listenerId) => ReadAsync<BoardSnapshot>(listenerId, FeedKind.Board);

    private async Task<T?> ReadAsync<T>(string listenerId, FeedKind kind)
        where T : class
    {
        var value = await database.StringGetAsync(RedisKeys.Feed(listenerId, kind));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value.ToString(), SentinelJson.Options);
    }
}
