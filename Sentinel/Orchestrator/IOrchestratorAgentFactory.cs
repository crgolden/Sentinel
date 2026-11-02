namespace Sentinel.Orchestrator;

using Microsoft.Agents.AI;

public interface IOrchestratorAgentFactory
{
    AIAgent Create();
}
