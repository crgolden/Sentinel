namespace Sentinel.Dispatch;

using Sentinel.Conversations;

public interface IDispatchQueue
{
    ValueTask EnqueueAsync(DispatchCommand command, CancellationToken cancellationToken);

    bool Cancel(ConversationKey conversation);
}
