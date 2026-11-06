namespace Sentinel.Tests.Unit.Dispatch;

using Microsoft.Extensions.Options;
using Sentinel.Conversations;
using Sentinel.Dispatch;

[Trait("Category", "Unit")]
public sealed class DispatchQueueTests
{
    private readonly DispatchQueue _queue = new(Options.Create(new DispatchOptions(Generated.NewCount(), Generated.NewCount(), Generated.NewCount())));
    private readonly ConversationKey _conversation = new(Generated.NewUserId(), Generated.LowercaseToken(8));

    [Fact]
    public async Task EnqueueAsync_MakesTheCommandReadable()
    {
        // Arrange
        var command = DispatchCommand.Message(_conversation, Generated.NewMessageText());

        // Act
        await _queue.EnqueueAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(command, await _queue.Reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Cancel_NothingRunning_ReturnsFalse()
    {
        // Act
        var canceled = _queue.Cancel(_conversation);

        // Assert
        Assert.False(canceled);
    }

    [Fact]
    public void Cancel_TrackedRun_CancelsItsToken()
    {
        // Arrange
        var source = _queue.Track(_conversation, TestContext.Current.CancellationToken);

        // Act
        var canceled = _queue.Cancel(_conversation);

        // Assert
        Assert.True(canceled);
        Assert.True(source.IsCancellationRequested);
        _queue.Untrack(_conversation, source);
    }

    [Fact]
    public void Untrack_FinishedRun_CannotBeCanceled()
    {
        // Arrange
        var source = _queue.Track(_conversation, TestContext.Current.CancellationToken);
        _queue.Untrack(_conversation, source);

        // Act
        var canceled = _queue.Cancel(_conversation);

        // Assert
        Assert.False(canceled);
    }
}
