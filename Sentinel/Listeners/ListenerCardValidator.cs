namespace Sentinel.Listeners;

using A2A;
using Sentinel.Feeds;

public static class ListenerCardValidator
{
    public static ListenerProblem Problem(AgentCard card, Uri baseAddress)
    {
        if (card.SupportedInterfaces is not { Count: > 0 } interfaces)
        {
            return ListenerProblem.NoInterfaces;
        }

        foreach (var agentInterface in interfaces)
        {
            if (!Uri.TryCreate(agentInterface.Url, UriKind.Absolute, out var url)
                || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                return ListenerProblem.InsecureInterface;
            }

            if (Uri.Compare(url, baseAddress, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return ListenerProblem.ForeignOrigin;
            }
        }

        return ListenerProblem.None;
    }

    public static bool SupportsFeeds(AgentCard card) =>
        card.Capabilities.Extensions is { } extensions
        && extensions.Any(extension => string.Equals(extension.Uri, FeedContract.ExtensionUri, StringComparison.Ordinal));
}
