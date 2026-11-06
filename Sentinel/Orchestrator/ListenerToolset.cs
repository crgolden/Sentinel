namespace Sentinel.Orchestrator;

using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Listeners;

public sealed class ListenerToolset(
    IListenerDirectory directory,
    IListenerRunner listenerRunner,
    OrchestratorOptions options,
    ConversationKey origin)
{
    public const string ListListenersName = "list_listeners";

    public const string AskListenerName = "ask_listener";

    public const string RelayToMasterName = "relay_to_master";

    public IList<AITool> Create()
    {
        List<AITool> tools =
        [
            AIFunctionFactory.Create(ListListeners, ListListenersName, options.ListListenersDescription),
            AIFunctionFactory.Create(AskListenerAsync, AskListenerName, options.AskListenerDescription),
        ];
        if (directory.Listeners.Any(listener => listener.Definition.Kind == ListenerKind.Master))
        {
            tools.Add(new ApprovalRequiredAIFunction(AIFunctionFactory.Create(RelayToMasterAsync, RelayToMasterName, options.RelayToMasterDescription)));
        }

        return tools;
    }

    public IReadOnlyList<ListenerSummary> ListListeners() =>
    [
        .. directory.Listeners.Select(listener => new ListenerSummary(
            listener.Id,
            listener.Definition.DisplayName,
            listener.Definition.Kind,
            listener.State,
            listener.Card?.Description,
            [.. (listener.Card?.Skills ?? []).Select(skill => skill.Name)])),
    ];

    public Task<ListenerToolResult> AskListenerAsync(
        [Description("The id of the listener, from list_listeners.")] string listenerId,
        [Description("The message for the listener.")] string message,
        CancellationToken cancellationToken)
    {
        var listener = directory.Find(listenerId);
        return listener is null || listener.Definition.Kind == ListenerKind.Master
            ? Task.FromResult(new ListenerToolResult(listenerId, RunState.Failed, options.ListenerNotFound, null))
            : RunAsync(listenerId, message, cancellationToken);
    }

    public Task<ListenerToolResult> RelayToMasterAsync(
        [Description("The message for the master session.")] string message,
        CancellationToken cancellationToken)
    {
        var master = directory.Listeners.FirstOrDefault(listener => listener.Definition.Kind == ListenerKind.Master);
        return master is null
            ? Task.FromResult(new ListenerToolResult(ListenerCatalog.OrchestratorTargetId, RunState.Failed, options.MasterNotConfigured, null))
            : RunAsync(master.Id, message, cancellationToken);
    }

    private async Task<ListenerToolResult> RunAsync(string listenerId, string message, CancellationToken cancellationToken)
    {
        var conversation = new ConversationKey(origin.OperatorId, listenerId);
        var result = await listenerRunner.RunAsync(DispatchCommand.Message(conversation, message, origin), cancellationToken);
        return new ListenerToolResult(listenerId, result.State, result.Text, result.Approval?.Id);
    }
}
