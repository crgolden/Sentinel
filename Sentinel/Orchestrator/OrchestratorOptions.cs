namespace Sentinel.Orchestrator;

public sealed record OrchestratorOptions(
    string Model,
    int MaxOutputTokens,
    string Instructions,
    string ListListenersDescription,
    string AskListenerDescription,
    string RelayToMasterDescription,
    string MasterNotConfigured,
    string ListenerNotFound);
