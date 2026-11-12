namespace Sentinel.Tests.Integration.TestSupport;

using System.Collections.Concurrent;
using System.Text.Json;
using A2A;
using Sentinel.Approvals;

public sealed class FakeListenerHandler : IAgentHandler
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, string> _pendingTools = new(StringComparer.Ordinal);

    public ConcurrentQueue<string> ReceivedText { get; } = new();

    public TaskCompletionSource SlowTaskStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource SlowTaskCanceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConcurrentQueue<string> ReceivedOperators { get; } = new();

    public ConcurrentQueue<ApprovalResponsePart> ReceivedDecisions { get; } = new();

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var contextId = context.ContextId;
        var updater = new TaskUpdater(eventQueue, context.TaskId, contextId);
        RecordOperator(context);
        if (context.IsContinuation)
        {
            await ContinueAsync(context, updater, cancellationToken);
            return;
        }

        var text = context.UserText ?? string.Concat(context.Message.Parts.Select(part => part.Text));
        ReceivedText.Enqueue(text);
        await updater.SubmitAsync(cancellationToken: cancellationToken);
        await updater.StartWorkAsync(cancellationToken: cancellationToken);
        if (text.StartsWith(IntegrationConstants.ApprovalPrefix, StringComparison.Ordinal))
        {
            var tool = text[IntegrationConstants.ApprovalPrefix.Length..].Trim();
            var approvalId = Guid.NewGuid().ToString("N");
            _pendingTools[approvalId] = tool;
            var argumentName = Generated.NewJsonPropertyName();
            var arguments = JsonSerializer.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal) { [argumentName] = tool });
            var request = JsonSerializer.SerializeToElement(
                new ApprovalRequestPart(ApprovalContract.RequestKind, approvalId, tool, arguments, ApprovalRisk.High, null),
                SentinelJson.Options);
            var prompt = Generated.NewMessageText();
            await updater.RequireInputAsync(
                new Message
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Role = Role.Agent,
                    ContextId = contextId,
                    TaskId = context.TaskId,
                    Parts = [Part.FromText(prompt), Part.FromData(request)],
                },
                cancellationToken: cancellationToken);
            return;
        }

        if (text.StartsWith(IntegrationConstants.SlowPrefix, StringComparison.Ordinal))
        {
            SlowTaskStarted.TrySetResult();
            await using var registration = cancellationToken.Register(() => SlowTaskCanceled.TrySetResult());
            await SlowTaskCanceled.Task;
            return;
        }

        await updater.AddArtifactAsync([Part.FromText($"{IntegrationConstants.EchoPrefix}{text}")], cancellationToken: cancellationToken);
        await updater.CompleteAsync(cancellationToken: cancellationToken);
    }

    public async Task CancelAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        SlowTaskCanceled.TrySetResult();
        var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
        await updater.CancelAsync(cancellationToken: cancellationToken);
    }

    private static ApprovalResponsePart? Decision(RequestContext context) =>
        context.Message.Parts
            .Select(part => part.Data)
            .OfType<JsonElement>()
            .Select(data => data.Deserialize<ApprovalResponsePart>(WebOptions))
            .OfType<ApprovalResponsePart>()
            .FirstOrDefault(part => string.Equals(part.Kind, ApprovalContract.ResponseKind, StringComparison.Ordinal));

    private void RecordOperator(RequestContext context)
    {
        if (context.Metadata is { } metadata && metadata.TryGetValue(ApprovalContract.OperatorMetadataKey, out var value))
        {
            ReceivedOperators.Enqueue(value.ToString());
        }
    }

    private async Task ContinueAsync(RequestContext context, TaskUpdater updater, CancellationToken cancellationToken)
    {
        if (Decision(context) is not { } decision || !_pendingTools.TryRemove(decision.ApprovalId, out var tool))
        {
            await updater.RejectAsync(cancellationToken: cancellationToken);
            return;
        }

        ReceivedDecisions.Enqueue(decision);
        var reply = decision.Approved ? IntegrationConstants.ApprovedReply : IntegrationConstants.RejectedReply;
        await updater.AddArtifactAsync([Part.FromText($"{reply} {tool}")], cancellationToken: cancellationToken);
        await updater.CompleteAsync(cancellationToken: cancellationToken);
    }
}
