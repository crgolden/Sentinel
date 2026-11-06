namespace Sentinel.Conversations;

using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public sealed class RedisConversationStore(IDatabase database, IOptions<ConversationOptions> options) : IConversationStore
{
    public async Task AppendAsync(ConversationKey key, TranscriptEntry entry)
    {
        var transcript = RedisKeys.Transcript(key);
        await database.ListRightPushAsync(transcript, JsonSerializer.Serialize(entry, SentinelJson.Options));
        await database.ListTrimAsync(transcript, -options.Value.MaxTranscriptEntries, -1);
    }

    public async Task<IReadOnlyList<TranscriptEntry>> GetTranscriptAsync(ConversationKey key)
    {
        var values = await database.ListRangeAsync(RedisKeys.Transcript(key));
        return [.. values.Select(value => JsonSerializer.Deserialize<TranscriptEntry>(value.ToString(), SentinelJson.Options)).OfType<TranscriptEntry>()];
    }

    public async Task<JsonElement?> LoadSessionAsync(ConversationKey key)
    {
        var value = await database.StringGetAsync(RedisKeys.Session(key));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<JsonElement>(value.ToString());
    }

    public Task SaveSessionAsync(ConversationKey key, JsonElement session) =>
        database.StringSetAsync(RedisKeys.Session(key), session.GetRawText());

    public async Task<RunRecord?> GetRunAsync(ConversationKey key)
    {
        var value = await database.StringGetAsync(RedisKeys.Run(key));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<RunRecord>(value.ToString(), SentinelJson.Options);
    }

    public async Task SaveRunAsync(ConversationKey key, RunRecord run)
    {
        await database.StringSetAsync(RedisKeys.Run(key), JsonSerializer.Serialize(run, SentinelJson.Options));
        var member = JsonSerializer.Serialize(key, SentinelJson.Options);
        if (run.State == RunState.Running)
        {
            await database.SetAddAsync(RedisKeys.ActiveRuns, member);
        }
        else
        {
            await database.SetRemoveAsync(RedisKeys.ActiveRuns, member);
        }
    }

    public async Task<IReadOnlyList<ConversationKey>> GetActiveRunsAsync()
    {
        var members = await database.SetMembersAsync(RedisKeys.ActiveRuns);
        return [.. members.Select(member => JsonSerializer.Deserialize<ConversationKey>(member.ToString(), SentinelJson.Options)).OfType<ConversationKey>()];
    }
}
