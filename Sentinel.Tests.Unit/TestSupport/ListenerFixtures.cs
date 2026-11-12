namespace Sentinel.Tests.Unit.TestSupport;

using System.Net.Mime;
using A2A;
using Sentinel.Feeds;
using Sentinel.Listeners;

internal static class ListenerFixtures
{
    internal static ListenerDefinition Definition(ListenerKind kind = ListenerKind.Tool, bool pinned = false) =>
        new(Generated.LowercaseToken(10), Generated.NewDisplayName(), new UriBuilder(Uri.UriSchemeHttps, Generated.NewHostname()).Uri, kind, pinned);

    internal static AgentCard Card(Uri interfaceBase, bool feeds = true) => new()
    {
        Name = Generated.LowercaseToken(8),
        Description = Generated.NewMessageText(),
        Version = Generated.LowercaseToken(4),
        Capabilities = new AgentCapabilities { Streaming = true, Extensions = feeds ? [new AgentExtension { Uri = FeedContract.ExtensionUri }] : [] },
        DefaultInputModes = [MediaTypeNames.Text.Plain],
        DefaultOutputModes = [MediaTypeNames.Text.Plain],
        Skills = [new AgentSkill { Id = Generated.LowercaseToken(6), Name = Generated.NewDisplayName(), Description = Generated.NewMessageText(), Tags = [] }],
        SupportedInterfaces = [new AgentInterface { Url = new Uri(interfaceBase, Generated.LowercaseToken(3)).AbsoluteUri, ProtocolBinding = ProtocolBindingNames.HttpJson, ProtocolVersion = A2AConstants.ProtocolVersion }],
    };

    internal static ListenerSnapshot Online(ListenerDefinition definition) =>
        new(definition, ListenerState.Online, ListenerProblem.None, Card(definition.BaseAddress), Generated.NewUtcTimestamp(), true);

    internal static ListenerSnapshot Offline(ListenerDefinition definition) =>
        new(definition, ListenerState.Offline, ListenerProblem.Unreachable, null, Generated.NewUtcTimestamp(), false);
}
