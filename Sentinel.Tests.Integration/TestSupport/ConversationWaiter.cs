namespace Sentinel.Tests.Integration.TestSupport;

using Sentinel.Conversations;
using Sentinel.Dispatch;

public sealed class ConversationWaiter : IDisposable
{
    private readonly TaskCompletionSource<RunState> _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _taskKnown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IDisposable _subscription;
    private int _runningUpdates;

    public ConversationWaiter(IConversationEvents events, ConversationKey conversation) =>
        _subscription = events.Subscribe(conversation, OnEvent);

    public Task<RunState> Settled => _settled.Task.WaitAsync(TestContext.Current.CancellationToken);

    public Task TaskKnown => _taskKnown.Task.WaitAsync(TestContext.Current.CancellationToken);

    public void Dispose() => _subscription.Dispose();

    private void OnEvent(ConversationEvent conversationEvent)
    {
        switch (conversationEvent)
        {
            case { Kind: ConversationEventKind.RunStateChanged, State: RunState.Running } when Interlocked.Increment(ref _runningUpdates) > 1:
                _taskKnown.TrySetResult();
                break;
            case { Kind: ConversationEventKind.RunStateChanged, State: { } state } when state != RunState.Running:
                _settled.TrySetResult(state);
                break;
        }
    }
}
