namespace Sentinel.Orchestrator;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Sentinel.Listeners;

public sealed class OrchestratorAgentFactory(IChatClient chatClient, IOptions<OrchestratorOptions> options) : IOrchestratorAgentFactory
{
    public AIAgent Create() =>
        new ChatClientAgent(
                chatClient,
                new ChatClientAgentOptions
                {
                    Id = ListenerCatalog.OrchestratorTargetId,
                    Name = ListenerCatalog.OrchestratorTargetId,
                    ChatOptions = new ChatOptions { Instructions = options.Value.Instructions },
                })
            .AsBuilder()
            .UseOpenTelemetry(Telemetry.Metrics.SourceName)
            .Build();
}
