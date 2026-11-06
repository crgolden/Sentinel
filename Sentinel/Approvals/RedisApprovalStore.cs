namespace Sentinel.Approvals;

using System.Text.Json;
using Sentinel.Conversations;
using StackExchange.Redis;

public sealed class RedisApprovalStore(IDatabase database) : IApprovalStore
{
    public Task AddAsync(PendingApproval approval) =>
        database.HashSetAsync(
            RedisKeys.Approvals(approval.Conversation.OperatorId),
            approval.Id.ToString("N"),
            JsonSerializer.Serialize(approval, SentinelJson.Options));

    public async Task<PendingApproval?> TakeAsync(Guid operatorId, Guid approvalId)
    {
        var key = RedisKeys.Approvals(operatorId);
        var field = approvalId.ToString("N");
        var value = await database.HashGetAsync(key, field);
        if (value.IsNullOrEmpty || !await database.HashDeleteAsync(key, field))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PendingApproval>(value.ToString(), SentinelJson.Options);
    }

    public async Task<IReadOnlyList<PendingApproval>> ListAsync(Guid operatorId)
    {
        var values = await database.HashValuesAsync(RedisKeys.Approvals(operatorId));
        return
        [
            .. values
                .Select(value => JsonSerializer.Deserialize<PendingApproval>(value.ToString(), SentinelJson.Options))
                .OfType<PendingApproval>()
                .OrderBy(approval => approval.RequestedAt),
        ];
    }
}
