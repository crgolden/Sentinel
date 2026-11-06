namespace Sentinel.Tests.Unit.Conversations;

using Sentinel.Conversations;
using Sentinel.Feeds;
using Sentinel.Listeners;

[Trait("Category", "Unit")]
public sealed class ConversationKeyTests
{
    [Fact]
    public void Orchestrator_TargetsTheReservedId()
    {
        // Arrange
        var operatorId = Generated.NewUserId();

        // Act
        var key = ConversationKey.Orchestrator(operatorId);

        // Assert
        Assert.True(key.IsOrchestrator);
        Assert.Equal(ListenerCatalog.OrchestratorTargetId, key.TargetId);
    }

    [Fact]
    public void IsOrchestrator_ListenerTarget_ReturnsFalse()
    {
        // Arrange
        var key = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));

        // Act
        var actual = key.IsOrchestrator;

        // Assert
        Assert.False(actual);
    }

    [Fact]
    public void ToString_JoinsTheOperatorAndTarget()
    {
        // Arrange
        var key = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));

        // Act
        var actual = key.ToString();

        // Assert
        Assert.Equal($"{key.OperatorId:N}:{key.TargetId}", actual);
    }

    [Fact]
    public void RedisKeys_AreScopedUnderThePrefix()
    {
        // Arrange
        var key = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));
        var listenerId = Generated.LowercaseToken(8);

        // Act
        string[] keys = [RedisKeys.Transcript(key), RedisKeys.Session(key), RedisKeys.Run(key), RedisKeys.Approvals(key.OperatorId), RedisKeys.Feed(listenerId, FeedKind.Board), RedisKeys.ActiveRuns, RedisKeys.Audit];

        // Assert
        Assert.All(keys, redisKey => Assert.StartsWith(RedisKeys.Prefix, redisKey, StringComparison.Ordinal));
        Assert.Equal($"{RedisKeys.Prefix}feeds:{listenerId}:board", keys[4]);
    }
}
