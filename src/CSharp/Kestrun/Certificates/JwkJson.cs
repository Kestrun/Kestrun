
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kestrun.Certificates;
/// <summary>
/// JSON serializer options for JWK serialization.
/// </summary>
public static class JwkJson
{
    /// <summary>
    /// JSON serializer options for JWK serialization.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
