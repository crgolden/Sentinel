namespace Sentinel.Listeners;

using System.Collections.Concurrent;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.A2A;

public sealed class ListenerAgentCache(IListenerDirectory directory, IHttpClientFactory httpClientFactory) : IListenerAgentProvider
{
    private readonly ConcurrentDictionary<string, CachedAgent> _agents = new(StringComparer.Ordinal);

    public AIAgent? GetAgent(string listenerId)
    {
        if (directory.Find(listenerId) is not { State: ListenerState.Online, Card: { } card } snapshot)
        {
            return null;
        }

        var cached = _agents.AddOrUpdate(
            listenerId,
            _ => Create(snapshot, card),
            (_, existing) => ReferenceEquals(existing.Card, card) ? existing : Create(snapshot, card));
        return cached.Agent;
    }

    private CachedAgent Create(ListenerSnapshot snapshot, AgentCard card)
    {
        var httpClient = httpClientFactory.CreateClient(ListenerHttpClientNames.A2A);
        var agent = card.AsAIAgent(
            new A2AAgentOptions { Id = snapshot.Id, Name = snapshot.Definition.DisplayName },
            httpClient);
        return new CachedAgent(card, agent);
    }
}
