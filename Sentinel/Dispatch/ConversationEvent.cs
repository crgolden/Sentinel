namespace Sentinel.Dispatch;

using Sentinel.Conversations;

public sealed record ConversationEvent(
    ConversationKey Conversation,
    ConversationEventKind Kind,
    TranscriptEntry? Entry,
    string? Delta,
    RunState? State);
