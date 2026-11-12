namespace Sentinel.Tests.Integration.TestSupport;

using System.Net;
using System.Net.Http.Json;
using Duende.IdentityModel;

public sealed class TokenEndpointHandler(string accessToken) : HttpMessageHandler
{
    public string AccessToken => accessToken;

    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        var lifetimeSeconds = Generated.NewExpiresInSeconds();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [OidcConstants.TokenResponse.AccessToken] = accessToken,
                [OidcConstants.TokenResponse.TokenType] = OidcConstants.TokenResponse.BearerTokenType,
                [OidcConstants.TokenResponse.ExpiresIn] = lifetimeSeconds,
            }),
        });
    }
}
