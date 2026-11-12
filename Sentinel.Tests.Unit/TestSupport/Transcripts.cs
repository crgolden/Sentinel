namespace Sentinel.Tests.Unit.TestSupport;

using Sentinel.Conversations;

internal static class Transcripts
{
    internal static TranscriptEntry Entry(TranscriptRole role, TranscriptEntryKind kind = TranscriptEntryKind.Message)
    {
        var entryId = Guid.NewGuid();
        return new TranscriptEntry(entryId, Generated.NewUtcTimestamp(), role, kind, Generated.NewMessageText(), null, null);
    }

    internal static RunRecord Run(RunState state)
    {
        var runId = Guid.NewGuid();
        return new RunRecord(runId, state, Generated.NewUtcTimestamp(), Generated.NewUtcTimestamp(), null, []);
    }
}
