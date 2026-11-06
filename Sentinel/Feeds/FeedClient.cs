namespace Sentinel.Feeds;

using System.Text;
using Microsoft.Extensions.Options;
using Sentinel.Listeners;

public sealed class FeedClient(IHttpClientFactory httpClientFactory, IOptions<FeedRefreshOptions> options) : IFeedClient
{
    public static Uri FeedUri(Uri baseAddress, FeedKind kind)
    {
        var root = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri($"{baseAddress.AbsoluteUri}/", UriKind.Absolute);
        var path = kind switch
        {
            FeedKind.Status => FeedContract.StatusPath,
            FeedKind.Artifacts => FeedContract.ArtifactsPath,
            FeedKind.Board => FeedContract.BoardPath,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return new Uri(root, path);
    }

    public async Task<string> GetAsync(ListenerDefinition listener, FeedKind kind, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(ListenerHttpClientNames.Feeds);
        using var response = await httpClient.GetAsync(FeedUri(listener.BaseAddress, kind), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var limit = options.Value.MaxFeedBytes;
        var buffer = new byte[limit + 1];
        var total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new InvalidOperationException($"The {kind} feed of listener '{listener.Id}' exceeds {limit} bytes.");
            }
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }
}
