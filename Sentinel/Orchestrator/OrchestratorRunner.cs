namespace Sentinel.Orchestrator;

using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Dispatch;

public sealed class OrchestratorRunner(
    IOrchestratorAgentFactory agentFactory,
    ListenerToolsetFactory toolsetFactory,
    IConversationStore store,
    ConversationJournal journal,
    ConversationLocks locks,
    ApprovalLedger ledger,
    Telemetry telemetry) : IOrchestratorRunner
{
    public async Task RunAsync(DispatchCommand command, CancellationToken cancellationToken)
    {
        var key = command.Conversation;
        using var activity = telemetry.StartActivity("sentinel.orchestrator.turn");
        using var conversationLock = await locks.AcquireAsync(key, cancellationToken);
        var agent = agentFactory.Create();
        var session = await store.LoadSessionAsync(key) is { } serialized
            ? await agent.DeserializeSessionAsync(serialized, SentinelJson.Options, cancellationToken)
            : await agent.CreateSessionAsync(cancellationToken);
        var message = await InputAsync(command);
        var run = await journal.StartRunAsync(key);
        var toolset = toolsetFactory.Create(key);
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions { Tools = toolset.Create() });
        var text = new StringBuilder();
        List<ToolApprovalRequestContent> approvalRequests = [];
        try
        {
            await foreach (var update in agent.RunStreamingAsync(message, session, runOptions, cancellationToken))
            {
                approvalRequests.AddRange(update.Contents.OfType<ToolApprovalRequestContent>());
                if (!IsNullOrWhiteSpace(update.Text))
                {
                    text.Append(update.Text);
                    journal.PublishDelta(key, update.Text);
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            await journal.AppendAsync(key, TranscriptRole.System, TranscriptEntryKind.Status, journal.Text.RunCanceled);
            await journal.SaveRunAsync(key, run with { State = RunState.Canceled });
            return;
        }
        catch (Exception ex)
        {
            telemetry.DispatchFailed(key.TargetId, ex);
            await journal.AppendAsync(key, TranscriptRole.System, TranscriptEntryKind.Error, journal.Text.RunFailed);
            await journal.SaveRunAsync(key, run with { State = RunState.Failed });
            return;
        }

        await store.SaveSessionAsync(key, await agent.SerializeSessionAsync(session, SentinelJson.Options, cancellationToken));
        if (text.Length > 0)
        {
            await journal.AppendAsync(key, TranscriptRole.Agent, TranscriptEntryKind.Message, text.ToString());
        }

        foreach (var request in approvalRequests)
        {
            await RequestApprovalAsync(key, request);
        }

        await journal.SaveRunAsync(key, run with { State = approvalRequests.Count > 0 ? RunState.AwaitingApproval : RunState.Completed });
    }

    internal static OrchestratorToolCall ToolCallOf(ToolApprovalRequestContent request)
    {
        var call = request.ToolCall as FunctionCallContent ?? throw new InvalidOperationException("Only function calls are approved by the operator.");
        return new OrchestratorToolCall(request.RequestId, call.CallId, call.Name, JsonSerializer.Serialize(call.Arguments, SentinelJson.Options));
    }

    internal static ToolApprovalResponseContent ResponseOf(OrchestratorToolCall toolCall, bool approved, string? reason)
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCall.ArgumentsJson, SentinelJson.Options);
        var request = new ToolApprovalRequestContent(toolCall.RequestId, new FunctionCallContent(toolCall.CallId, toolCall.Name, arguments));
        return request.CreateResponse(approved, reason);
    }

    private async Task<ChatMessage> InputAsync(DispatchCommand command)
    {
        var key = command.Conversation;
        if (command.Kind == DispatchCommandKind.ApprovalDecision
            && command.Approval is { ToolCall: { } toolCall } approval)
        {
            await journal.AppendAsync(
                key,
                TranscriptRole.Operator,
                TranscriptEntryKind.ApprovalDecided,
                command.Approved ? journal.Text.ApprovalGranted : journal.Text.ApprovalRejected,
                approval.Id);
            return new ChatMessage(ChatRole.User, [ResponseOf(toolCall, command.Approved, command.Reason)]);
        }

        var text = command.Text ?? throw new InvalidOperationException("A message command carries text.");
        await journal.AppendAsync(key, TranscriptRole.Operator, TranscriptEntryKind.Message, text);
        await ledger.RecordAsync(key, AuditAction.MessageDispatched);
        return new ChatMessage(ChatRole.User, text);
    }

    private Task<PendingApproval> RequestApprovalAsync(ConversationKey key, ToolApprovalRequestContent request)
    {
        var toolCall = ToolCallOf(request);
        var approval = new PendingApproval(
            Guid.NewGuid(),
            ApprovalKind.OrchestratorTool,
            key,
            null,
            toolCall.Name,
            toolCall.ArgumentsJson,
            ApprovalRisk.High,
            toolCall.Name,
            ledger.Now,
            null,
            null,
            toolCall);
        return ledger.OpenAsync(approval, journal.Text.ApprovalPending);
    }
}
