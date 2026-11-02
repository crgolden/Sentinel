namespace Sentinel.Conversations;

public interface IConversationReader
{
    Task<IReadOnlyList<TranscriptEntry>> GetTranscriptAsync(ConversationKey key);

    Task<RunRecord?> GetRunAsync(ConversationKey key);
}
