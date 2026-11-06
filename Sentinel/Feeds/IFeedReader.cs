namespace Sentinel.Feeds;

public interface IFeedReader
{
    Task<ListenerStatusFeed?> GetStatusAsync(string listenerId);

    Task<ArtifactFeed?> GetArtifactsAsync(string listenerId);

    Task<BoardSnapshot?> GetBoardAsync(string listenerId);
}
