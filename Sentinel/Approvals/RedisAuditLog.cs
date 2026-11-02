namespace Sentinel.Approvals;

using System.Text.Json;
using Sentinel.Conversations;
using StackExchange.Redis;

public sealed class RedisAuditLog(IDatabase database) : IAuditLog
{
    internal const string EntryField = "entry";

    public Task RecordAsync(AuditEntry entry) =>
        database.StreamAddAsync(RedisKeys.Audit, EntryField, JsonSerializer.Serialize(entry, SentinelJson.Options));

    public async Task<IReadOnlyList<AuditEntry>> ReadLatestAsync(int count)
    {
        var entries = await database.StreamRangeAsync(RedisKeys.Audit, count: count, messageOrder: Order.Descending);
        return
        [
            .. entries
                .Select(entry => entry[EntryField])
                .Where(value => !value.IsNullOrEmpty)
                .Select(value => JsonSerializer.Deserialize<AuditEntry>(value.ToString(), SentinelJson.Options))
                .OfType<AuditEntry>(),
        ];
    }
}
