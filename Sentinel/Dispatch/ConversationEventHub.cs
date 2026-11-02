namespace Sentinel.Dispatch;

using System.Collections.Immutable;
using Sentinel.Conversations;

public sealed class ConversationEventHub : IConversationEvents
{
    private ImmutableList<Subscription> _subscriptions = [];

    public IDisposable Subscribe(ConversationKey conversation, Action<ConversationEvent> handler) =>
        Add(new Subscription(this, e => e.Conversation.Equals(conversation), handler));

    public IDisposable SubscribeAll(Guid operatorId, Action<ConversationEvent> handler) =>
        Add(new Subscription(this, e => e.Conversation.OperatorId == operatorId, handler));

    public void Publish(ConversationEvent conversationEvent)
    {
        foreach (var subscription in _subscriptions.Where(subscription => subscription.Filter(conversationEvent)))
        {
            subscription.Handler(conversationEvent);
        }
    }

    private Subscription Add(Subscription subscription)
    {
        ImmutableInterlocked.Update(ref _subscriptions, list => list.Add(subscription));
        return subscription;
    }

    private void Remove(Subscription subscription) =>
        ImmutableInterlocked.Update(ref _subscriptions, list => list.Remove(subscription));

    private sealed class Subscription(ConversationEventHub hub, Func<ConversationEvent, bool> filter, Action<ConversationEvent> handler) : IDisposable
    {
        public Func<ConversationEvent, bool> Filter { get; } = filter;

        public Action<ConversationEvent> Handler { get; } = handler;

        public void Dispose() => hub.Remove(this);
    }
}
