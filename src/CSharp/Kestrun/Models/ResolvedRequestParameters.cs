using Kestrun.Languages;

namespace Kestrun.Models;
/// <summary>
///  Resolved parameters for a request.
/// </summary>
public record ResolvedRequestParameters
{
    /// <summary>
    /// The body of the request, if any.
    /// </summary>
    public ParameterForInjectionResolved? Body { get; set; }
    /// <summary>
    /// The resolved parameters for the operation.
    /// </summary>
    public Dictionary<string, ParameterForInjectionResolved> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
