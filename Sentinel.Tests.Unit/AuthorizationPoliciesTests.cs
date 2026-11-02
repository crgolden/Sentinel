namespace Sentinel.Tests.Unit;

using System.Security.Claims;

[Trait("Category", "Unit")]
public sealed class AuthorizationPoliciesTests
{
    [Fact]
    public void OperatorId_GuidSubject_ReturnsTheGuid()
    {
        // Arrange
        var operatorId = Generated.NewUserId();
        var principal = Principal(new Claim(AuthorizationPolicies.SubjectClaimType, operatorId.ToString()));

        // Act
        var actual = AuthorizationPolicies.OperatorId(principal);

        // Assert
        Assert.Equal(operatorId, actual);
    }

    [Fact]
    public void OperatorId_NonGuidSubject_ReturnsNull()
    {
        // Arrange
        var principal = Principal(new Claim(AuthorizationPolicies.SubjectClaimType, Generated.LowercaseToken(10)));

        // Act
        var actual = AuthorizationPolicies.OperatorId(principal);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void IsOperator_SubjectAndOperatorRole_ReturnsTrue()
    {
        // Arrange
        var principal = Principal(
            new Claim(AuthorizationPolicies.SubjectClaimType, Generated.NewUserId().ToString()),
            new Claim(AuthorizationPolicies.RoleClaimType, AuthorizationPolicies.OperatorRole));

        // Act
        var actual = AuthorizationPolicies.IsOperator(principal);

        // Assert
        Assert.True(actual);
    }

    [Fact]
    public void IsOperator_SubjectWithAnotherRole_ReturnsFalse()
    {
        // Arrange
        var principal = Principal(
            new Claim(AuthorizationPolicies.SubjectClaimType, Generated.NewUserId().ToString()),
            new Claim(AuthorizationPolicies.RoleClaimType, Generated.LowercaseToken(8)));

        // Act
        var actual = AuthorizationPolicies.IsOperator(principal);

        // Assert
        Assert.False(actual);
    }

    [Fact]
    public void Constants_MatchTheIdentityRegistration()
    {
        // Act
        var constants = (AuthorizationPolicies.OperatorRole, AuthorizationPolicies.SentinelIdentityScope, AuthorizationPolicies.RoleClaimType, AuthorizationPolicies.SubjectClaimType);

        // Assert
        Assert.Equal(("sentinel.operator", "sentinel", "role", "sub"), constants);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, Generated.LowercaseToken(6)));
}
