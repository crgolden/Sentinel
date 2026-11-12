namespace Sentinel.Tests.Integration.TestSupport;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

public sealed class ScriptedChatClient : IChatClient
{
    private readonly ConcurrentQueue<ChatResponseUpdate> _script = new();

    public void Reply(string text) => _script.Enqueue(new ChatResponseUpdate(ChatRole.Assistant, text));

    public void CallTool(string name, IDictionary<string, object?> arguments)
    {
        var callId = Guid.NewGuid().ToString("N");
        _script.Enqueue(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)]));
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatResponseUpdate> updates = [];
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
        }

        return updates.ToChatResponse();
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var update = _script.TryDequeue(out var scripted) ? scripted : new ChatResponseUpdate(ChatRole.Assistant, nameof(ScriptedChatClient));
        yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
