namespace Sentinel.Listeners;

using System.Text.RegularExpressions;

public static partial class ListenerCatalog
{
    public const string OrchestratorTargetId = "sentinel";

    public static IReadOnlyList<ListenerDefinition> Validate(IReadOnlyList<ListenerDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (!IdPattern().IsMatch(definition.Id) || string.Equals(definition.Id, OrchestratorTargetId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid listener id '{definition.Id}'.");
            }

            if (!definition.BaseAddress.IsAbsoluteUri || !string.Equals(definition.BaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Listener '{definition.Id}' must have an absolute https base address.");
            }
        }

        var duplicate = definitions.GroupBy(definition => definition.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Listener id '{duplicate.Key}' is configured more than once.");
        }

        if (definitions.Count(definition => definition.Kind == ListenerKind.Master) > 1)
        {
            throw new InvalidOperationException("At most one listener may be the master bridge.");
        }

        return definitions;
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
