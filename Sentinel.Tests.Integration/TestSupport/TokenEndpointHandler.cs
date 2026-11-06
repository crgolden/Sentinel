namespace Sentinel.Tests.Integration.TestSupport;

using System.Net;
using System.Net.Http.Json;

public sealed class TokenEndpointHandler(string accessToken) : HttpMessageHandler
{
    public string AccessToken => accessToken;

    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new Dictionary<string, object>
            {
                ["access_token"] = accessToken,
                ["token_type"] = "Bearer",
                ["expires_in"] = 3600,
                ["scope"] = "listeners",
            }),
        });
    }
}
