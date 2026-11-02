namespace Sentinel;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

internal static class AuthorizationPolicies
{
    internal const string Operator = nameof(Operator);

    internal const string SubjectClaimType = JwtRegisteredClaimNames.Sub;

    internal const string RoleClaimType = "role";

    internal const string OperatorRole = "sentinel.operator";

    internal const string SentinelIdentityScope = "sentinel";

    internal static Guid? OperatorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(SubjectClaimType)?.Value, out var operatorId) ? operatorId : null;

    internal static bool IsOperator(ClaimsPrincipal principal) =>
        OperatorId(principal) is not null && principal.HasClaim(RoleClaimType, OperatorRole);
}
