namespace Sentinel;

using System.Text.Json;
using System.Text.Json.Serialization;

internal static class SentinelJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };
}
