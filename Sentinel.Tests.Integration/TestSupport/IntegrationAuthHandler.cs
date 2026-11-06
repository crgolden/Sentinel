namespace Sentinel.Tests.Integration.TestSupport;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class IntegrationAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public IntegrationAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(IntegrationConstants.OperatorHeader, out var operatorId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims = [new Claim(AuthorizationPolicies.SubjectClaimType, operatorId.ToString())];
        claims.AddRange(Request.Headers[IntegrationConstants.RoleHeader].OfType<string>().Select(role => new Claim(AuthorizationPolicies.RoleClaimType, role)));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
