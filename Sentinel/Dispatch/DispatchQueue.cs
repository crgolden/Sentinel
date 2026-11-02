namespace Sentinel.Dispatch;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Sentinel.Conversations;

public sealed class DispatchQueue(IOptions<DispatchOptions> options) : IDispatchQueue
{
    private readonly Channel<DispatchCommand> _channel = Channel.CreateBounded<DispatchCommand>(
        new BoundedChannelOptions(options.Value.QueueCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly ConcurrentDictionary<ConversationKey, CancellationTokenSource> _running = new();

    internal ChannelReader<DispatchCommand> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(DispatchCommand command, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(command, cancellationToken);

    public bool Cancel(ConversationKey conversation)
    {
        if (!_running.TryGetValue(conversation, out var source))
        {
            return false;
        }

        source.Cancel();
        return true;
    }

    internal CancellationTokenSource Track(ConversationKey conversation, CancellationToken stoppingToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _running[conversation] = source;
        return source;
    }

    internal void Untrack(ConversationKey conversation, CancellationTokenSource source)
    {
        _running.TryRemove(new KeyValuePair<ConversationKey, CancellationTokenSource>(conversation, source));
        source.Dispose();
    }
}
