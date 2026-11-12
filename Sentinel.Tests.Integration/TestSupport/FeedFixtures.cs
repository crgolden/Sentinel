namespace Sentinel.Tests.Integration.TestSupport;

using System.Text.Json;
using Sentinel.Feeds;

internal static class FeedFixtures
{
    internal static FeedPayloads Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        var status = new ListenerStatusFeed
        {
            GeneratedAt = Generated.NewUtcTimestamp(),
            Health = ListenerHealth.Degraded,
            Summary = Generated.NewMessageText(),
            Checks = [new StatusCheck(Generated.NewDisplayName(), ListenerHealth.Healthy, Generated.NewMessageText(), Generated.NewUtcTimestamp())],
        };
        var artifacts = new ArtifactFeed
        {
            GeneratedAt = Generated.NewUtcTimestamp(),
            Items = [new ArtifactItem(Generated.LowercaseToken(12), Generated.NewDisplayName(), ArtifactKind.AuditReport, Generated.NewUtcTimestamp(), Generated.NewMessageText(), null, Generated.NewMessageText())],
        };
        var sessionId = Guid.NewGuid().ToString();
        var board = new BoardSnapshot
        {
            GeneratedAt = Generated.NewUtcTimestamp(),
            Sessions = [new SessionRow(sessionId, Generated.NewHostLabel(), Generated.NewMessageText(), null, Generated.NewUtcTimestamp())],
            Audits = [new AuditRow(Generated.LowercaseToken(8), Generated.NewUtcTimestamp(), Generated.NewMessageText(), null, Generated.NewUtcTimestamp())],
        };
        return new FeedPayloads(
            JsonSerializer.Serialize(status, options),
            JsonSerializer.Serialize(artifacts, options),
            JsonSerializer.Serialize(board, options));
    }
}
