namespace Sentinel.Dispatch;

using Microsoft.Extensions.Options;
using Sentinel.Conversations;

public sealed class ConversationJournal(
    IConversationStore store,
    ConversationEventHub hub,
    TimeProvider timeProvider,
    IOptions<TranscriptTextOptions> text)
{
    public TranscriptTextOptions Text => text.Value;

    public async Task<TranscriptEntry> AppendAsync(
        ConversationKey key,
        TranscriptRole role,
        TranscriptEntryKind kind,
        string content,
        Guid? approvalId = null)
    {
        var entry = new TranscriptEntry(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            role,
            kind,
            content,
            key.IsOrchestrator ? null : key.TargetId,
            approvalId);
        await store.AppendAsync(key, entry);
        hub.Publish(new ConversationEvent(key, ConversationEventKind.EntryAppended, entry, null, null));
        return entry;
    }

    public void PublishDelta(ConversationKey key, string delta) =>
        hub.Publish(new ConversationEvent(key, ConversationEventKind.Delta, null, delta, null));

    public async Task<RunRecord> StartRunAsync(ConversationKey key)
    {
        var now = timeProvider.GetUtcNow();
        var run = new RunRecord(Guid.NewGuid(), RunState.Running, now, now, null, null);
        await SaveRunAsync(key, run);
        return run;
    }

    public async Task<RunRecord> SaveRunAsync(ConversationKey key, RunRecord run)
    {
        var updated = run with { UpdatedAt = timeProvider.GetUtcNow() };
        await store.SaveRunAsync(key, updated);
        hub.Publish(new ConversationEvent(key, ConversationEventKind.RunStateChanged, null, null, updated.State));
        return updated;
    }
}
