namespace Sentinel.Listeners;

using Microsoft.Agents.AI;

public interface IListenerAgentProvider
{
    AIAgent? GetAgent(string listenerId);
}
