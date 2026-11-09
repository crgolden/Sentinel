namespace Sentinel.Dispatch;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.A2A;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Listeners;

public sealed class ListenerRunner(
    IListenerAgentProvider agentProvider,
    IConversationStore store,
    ConversationJournal journal,
    ConversationLocks locks,
    ApprovalLedger ledger,
    IOptions<DispatchOptions> dispatchOptions,
    Telemetry telemetry) : IListenerRunner
{
    public async Task<ListenerTurnResult> RunAsync(DispatchCommand command, CancellationToken cancellationToken)
    {
        var key = command.Conversation;
        using var activity = Telemetry.ActivitySource.CreateActivity("sentinel.listener.turn", ActivityKind.Internal)?.Start();
        activity?.SetTag(Telemetry.Metrics.ListenerTagName, key.TargetId);
        using var conversationLock = await locks.AcquireAsync(key, cancellationToken);
        if (agentProvider.GetAgent(key.TargetId) is not A2AAgent agent)
        {
            await journal.AppendAsync(key, TranscriptRole.System, TranscriptEntryKind.Error, journal.Text.ListenerUnavailable);
            return new ListenerTurnResult(RunState.Failed, null, null);
        }

        var previousRun = await store.GetRunAsync(key);
        var session = await LoadSessionAsync(agent, key, command.Kind != DispatchCommandKind.Resume);
        var run = command.Kind == DispatchCommandKind.Resume && previousRun is not null ? previousRun : await journal.StartRunAsync(key);
        var options = new AgentRunOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { [ApprovalContract.OperatorMetadataKey] = key.OperatorId.ToString() },
        };
        IReadOnlyList<ChatMessage> messages = [];
        switch (command.Kind)
        {
            case DispatchCommandKind.Message:
                await journal.AppendAsync(key, TranscriptRole.Operator, TranscriptEntryKind.Message, command.Text ?? throw new InvalidOperationException("A message command carries text."));
                await ledger.RecordAsync(key, AuditAction.MessageDispatched);
                messages = [new ChatMessage(ChatRole.User, command.Text)];
                break;
            case DispatchCommandKind.ApprovalDecision:
                var approval = command.Approval ?? throw new InvalidOperationException("A decision command carries its approval.");
                await journal.AppendAsync(
                    key,
                    TranscriptRole.Operator,
                    TranscriptEntryKind.ApprovalDecided,
                    command.Approved ? journal.Text.ApprovalGranted : journal.Text.ApprovalRejected,
                    approval.Id);
                messages =
                [
                    new ChatMessage(
                        ChatRole.User,
                        [
                            new TextContent(command.Approved ? journal.Text.ApprovalGranted : journal.Text.ApprovalRejected),
                            new AIContent { RawRepresentation = ListenerUpdateClassifier.ResponsePart(approval, command.Approved, command.Reason) },
                        ]),
                ];
                break;
            case DispatchCommandKind.Resume when run.ContinuationToken is { } token:
                options.ContinuationToken = ResponseContinuationToken.FromBytes(token);
                break;
            default:
                await journal.AppendAsync(key, TranscriptRole.System, TranscriptEntryKind.Status, journal.Text.RunInterrupted);
                await journal.SaveRunAsync(key, run with { State = RunState.Interrupted });
                return new ListenerTurnResult(RunState.Interrupted, null, null);
        }

        return await StreamAsync(agent, session, messages, options, command, run, cancellationToken);
    }

    private async Task CancelRemoteTaskAsync(A2AAgent agent, RunRecord run)
    {
        if (run.TaskId is not { } taskId || agent.GetService(typeof(IA2AClient)) is not IA2AClient client)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(dispatchOptions.Value.CancelTimeoutSeconds));
        try
        {
            await client.CancelTaskAsync(new CancelTaskRequest { Id = taskId }, timeout.Token);
        }
        catch (Exception ex)
        {
            Activity.Current?.AddException(ex);
        }
    }

    private async Task<AgentSession> LoadSessionAsync(A2AAgent agent, ConversationKey key, bool allowFresh)
    {
        if (await store.LoadSessionAsync(key) is not { } serialized)
        {
            return await agent.CreateSessionAsync();
        }

        var session = await agent.DeserializeSessionAsync(serialized, SentinelJson.Options);
        return allowFresh && session is A2AAgentSession { ContextId: { } contextId } a2aSession && SessionPolicy.NeedsFreshSession(a2aSession.TaskState)
            ? await agent.CreateSessionAsync(contextId)
            : session;
    }

    private async Task<ListenerTurnResult> StreamAsync(
        A2AAgent agent,
        AgentSession session,
        IReadOnlyList<ChatMessage> messages,
        AgentRunOptions options,
        DispatchCommand command,
        RunRecord run,
        CancellationToken cancellationToken)
    {
        var key = command.Conversation;
        var stream = new ListenerStream();
        var remoteCancel = Task.CompletedTask;
        var current = new RunCursor(run);
        await using var cancelRegistration = cancellationToken.Register(() => remoteCancel = CancelRemoteTaskAsync(agent, current.Run));
        try
        {
            await foreach (var update in agent.RunStreamingAsync(messages, session, options, cancellationToken))
            {
                var classified = stream.Apply(update, delta => journal.PublishDelta(key, delta));
                if (run.TaskId is null && classified.TaskId is { } taskId)
                {
                    run = await journal.SaveRunAsync(key, run with { TaskId = taskId, ContinuationToken = update.ContinuationToken?.ToBytes().ToArray() });
                    current.Run = run;
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return await CanceledAsync(agent, session, key, run, remoteCancel);
        }
        catch (Exception ex)
        {
            telemetry.DispatchFailed(key.TargetId, ex);
            await journal.AppendAsync(key, TranscriptRole.System, TranscriptEntryKind.Error, journal.Text.RunFailed);
            await journal.SaveRunAsync(key, run with { State = RunState.Failed });
            return new ListenerTurnResult(RunState.Failed, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return await CanceledAsync(agent, session, key, run, remoteCancel);
        }

        await store.SaveSessionAsync(key, await agent.SerializeSessionAsync(session, SentinelJson.Options, cancellationToken));
        var reply = stream.Reply;
        if (reply is not null)
        {
            await journal.AppendAsync(key, TranscriptRole.Agent, TranscriptEntryKind.Message, reply);
        }

        var pending = stream is { State: TaskState.InputRequired, Approval: { } approvalRequest }
            ? await RequestApprovalAsync(command, approvalRequest, stream.StatusText)
            : null;
        var outcome = SessionPolicy.Outcome(stream.State, pending is not null);
        await journal.SaveRunAsync(key, run with { State = outcome, ContinuationToken = null });
        if (command is { Kind: DispatchCommandKind.ApprovalDecision, Origin: { } origin } && reply is not null)
        {
            await journal.AppendAsync(origin, TranscriptRole.Agent, TranscriptEntryKind.Status, Format(CultureInfo.InvariantCulture, journal.Text.ListenerRepliedAfterApproval, key.TargetId, reply));
        }

        return new ListenerTurnResult(outcome, reply, pending);
    }

    private async Task<ListenerTurnResult> CanceledAsync(A2AAgent agent, AgentSession session, ConversationKey key, RunRecord run, Task remoteCancel)
    {
        await remoteCancel;
        await CancelRemoteTaskAsync(agent, run);
        await store.SaveSessionAsync(key, await agent.SerializeSessionAsync(session, SentinelJson.Options, CancellationToken.None));
        await journal.AppendAsync(key, TranscriptRole.System, TranscriptEntryKind.Status, journal.Text.RunCanceled);
        await ledger.RecordAsync(key, AuditAction.RunCanceled);
        await journal.SaveRunAsync(key, run with { State = RunState.Canceled });
        return new ListenerTurnResult(RunState.Canceled, null, null);
    }

    private Task<PendingApproval> RequestApprovalAsync(DispatchCommand command, ApprovalRequestPart request, string? statusText)
    {
        var key = command.Conversation;
        var approval = new PendingApproval(
            Guid.NewGuid(),
            ApprovalKind.ListenerInput,
            key,
            command.Origin,
            request.Tool,
            request.Arguments is { } arguments ? arguments.GetRawText() : JsonSerializer.Serialize(new object(), SentinelJson.Options),
            request.Risk,
            IsNullOrWhiteSpace(statusText) ? request.Tool : statusText,
            ledger.Now,
            request.ExpiresAt,
            request.ApprovalId,
            null);
        return ledger.OpenAsync(approval, approval.Summary);
    }
}
