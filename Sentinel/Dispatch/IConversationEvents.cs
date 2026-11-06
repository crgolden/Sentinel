namespace Sentinel.Dispatch;

using Sentinel.Conversations;

public interface IConversationEvents
{
    IDisposable Subscribe(ConversationKey conversation, Action<ConversationEvent> handler);

    IDisposable SubscribeAll(Guid operatorId, Action<ConversationEvent> handler);
}
