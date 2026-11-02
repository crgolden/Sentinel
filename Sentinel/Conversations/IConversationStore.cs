namespace Sentinel.Conversations;

using System.Text.Json;

public interface IConversationStore : IConversationReader
{
    Task AppendAsync(ConversationKey key, TranscriptEntry entry);

    Task<JsonElement?> LoadSessionAsync(ConversationKey key);

    Task SaveSessionAsync(ConversationKey key, JsonElement session);

    Task SaveRunAsync(ConversationKey key, RunRecord run);

    Task<IReadOnlyList<ConversationKey>> GetActiveRunsAsync();
}
