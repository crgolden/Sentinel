namespace Sentinel.Listeners;

using A2A;

public interface IAgentCardSource
{
    Task<AgentCard> GetCardAsync(Uri baseAddress, CancellationToken cancellationToken);
}
