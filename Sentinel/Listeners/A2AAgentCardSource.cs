namespace Sentinel.Listeners;

using A2A;
using Microsoft.Extensions.Options;

public sealed class A2AAgentCardSource(IHttpClientFactory httpClientFactory, IOptions<ListenerRefreshOptions> options) : IAgentCardSource
{
    public async Task<AgentCard> GetCardAsync(Uri baseAddress, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(ListenerHttpClientNames.Cards);
        var resolver = new A2ACardResolver(baseAddress, options.Value.MaxCardBytes, httpClient);
        return await resolver.GetAgentCardAsync(cancellationToken);
    }
}
