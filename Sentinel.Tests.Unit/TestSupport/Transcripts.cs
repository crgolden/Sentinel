namespace Sentinel.Tests.Unit.TestSupport;

using Sentinel.Conversations;

internal static class Transcripts
{
    internal static TranscriptEntry Entry(TranscriptRole role, TranscriptEntryKind kind = TranscriptEntryKind.Message) =>
        new(Guid.NewGuid(), Generated.NewUtcTimestamp(), role, kind, Generated.NewMessageText(), null, null);

    internal static RunRecord Run(RunState state) =>
        new(Guid.NewGuid(), state, Generated.NewUtcTimestamp(), Generated.NewUtcTimestamp(), null, null);
}
