namespace Sentinel.Orchestrator;

using Microsoft.Extensions.Options;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Listeners;

public sealed class ListenerToolsetFactory(IListenerDirectory directory, IListenerRunner listenerRunner, IOptions<OrchestratorOptions> options)
{
    public ListenerToolset Create(ConversationKey origin) => new(directory, listenerRunner, options.Value, origin);
}
