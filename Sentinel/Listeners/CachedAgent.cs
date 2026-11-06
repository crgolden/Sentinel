namespace Sentinel.Listeners;

using A2A;
using Microsoft.Agents.AI;

internal sealed record CachedAgent(AgentCard Card, AIAgent Agent);
