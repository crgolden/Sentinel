namespace Sentinel.Tests.Integration.TestSupport;

internal static class IntegrationConstants
{
    internal const string CollectionName = "Integration";

    internal const int TestDatabase = 1;

    internal const string OperatorHeader = "X-Integration-Operator";

    internal const string RoleHeader = "X-Integration-Role";

    internal const string ApprovalPrefix = "approve:";

    internal const string EchoPrefix = "echo: ";

    internal const string SlowPrefix = "slow:";

    internal const string ApprovedReply = "approved";

    internal const string RejectedReply = "rejected";
}
