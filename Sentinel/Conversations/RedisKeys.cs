namespace Sentinel.Conversations;

using Sentinel.Feeds;

public static class RedisKeys
{
    public const string Prefix = "sentinel:";

    public const string ActiveRuns = Prefix + "runs:active";

    public const string Audit = Prefix + "audit";

    public const string FeedsSegment = "feeds";

    public static string Transcript(ConversationKey key) => $"{Prefix}conversations:{key}:transcript";

    public static string Session(ConversationKey key) => $"{Prefix}conversations:{key}:session";

    public static string Run(ConversationKey key) => $"{Prefix}conversations:{key}:run";

    public static string Approvals(Guid operatorId) => $"{Prefix}approvals:{operatorId:N}";

    public static string Feed(string listenerId, FeedKind kind) => $"{Prefix}{FeedsSegment}:{listenerId}:{FeedSegment(kind)}";

    public static string FeedSegment(FeedKind kind) => kind.ToString().ToLowerInvariant();
}
