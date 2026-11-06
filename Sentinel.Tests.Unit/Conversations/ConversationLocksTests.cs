namespace Sentinel.Tests.Unit.Conversations;

using Sentinel.Conversations;

[Trait("Category", "Unit")]
public sealed class ConversationLocksTests
{
    private readonly ConversationLocks _locks = new();

    [Fact]
    public async Task AcquireAsync_SameConversationWhileHeld_WaitsForTheRelease()
    {
        // Arrange
        var key = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));
        var held = await _locks.AcquireAsync(key, TestContext.Current.CancellationToken);
        var waiting = _locks.AcquireAsync(key, TestContext.Current.CancellationToken);
        var waitedBeforeRelease = waiting.IsCompleted;

        // Act
        held.Dispose();

        // Assert
        using var acquired = await waiting;
        Assert.False(waitedBeforeRelease);
    }

    [Fact]
    public async Task AcquireAsync_DifferentConversations_DoNotBlockEachOther()
    {
        // Arrange
        var operatorId = Generated.NewUserId();
        using var held = await _locks.AcquireAsync(new ConversationKey(operatorId, Generated.LowercaseToken(8)), TestContext.Current.CancellationToken);

        // Act
        var other = _locks.AcquireAsync(new ConversationKey(operatorId, Generated.LowercaseToken(9)), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(other.IsCompletedSuccessfully);
        (await other).Dispose();
    }
}
