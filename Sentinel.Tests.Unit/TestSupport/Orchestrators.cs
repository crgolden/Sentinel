namespace Sentinel.Tests.Unit.TestSupport;

using Sentinel.Orchestrator;

internal static class Orchestrators
{
    internal static OrchestratorOptions Options() => new(
        Generated.LowercaseToken(10),
        Generated.NewCount(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText(),
        Generated.NewMessageText());
}
