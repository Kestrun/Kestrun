using Kestrun.Hosting;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for OpenAPI integration.
/// </summary>
public static class OpenApiHelper
{
    /// <summary>
    /// Adds a security requirement object to the OpenAPI metadata based on the specified scheme and policies.
    /// </summary>
    /// <param name="host"> The Kestrun host instance.</param>
    /// <param name="scheme">The security scheme name.</param>
    /// <param name="policyList">List of security policies.</param>
    /// <param name="securitySchemes">The list of security schemes to which the security requirement will be added.</param>
    /// <returns>A list of all security schemes involved in the requirement.</returns>
    internal static List<string> AddSecurityRequirementObject(this KestrunHost host, string? scheme, List<string> policyList, List<Dictionary<string, List<string>>> securitySchemes)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(policyList);
        ArgumentNullException.ThrowIfNull(securitySchemes);
        var allSchemes = new List<string>();
        // Ensure Security list exists:
        // List<Dictionary<string, IEnumerable<string>>>

        var tempScopesByScheme = new Dictionary<string, List<string>>();

        // Start with the explicit schema, if any
        if (!string.IsNullOrWhiteSpace(scheme))
        {
            tempScopesByScheme[scheme] = [];
            if (!allSchemes.Contains(scheme))
            {
                allSchemes.Add(scheme);
            }
        }
        // For each policy, resolve schemes and map scheme -> scopes (policies)
        foreach (var policy in policyList)
        {
            var schemesForPolicy = host.RegisteredAuthentications.GetSchemesByPolicy(policy);
            if (schemesForPolicy is null) { continue; }

            foreach (var sc in schemesForPolicy)
            {
                if (!tempScopesByScheme.TryGetValue(sc, out var scopeList))
                {
                    scopeList = [];
                    tempScopesByScheme[sc] = scopeList;
                }
                if (scopeList != null && scopeList is List<string> list && !list.Contains(policy))
                {
                    list.Add(policy);
                }

                if (!allSchemes.Contains(sc))
                {
                    allSchemes.Add(sc);
                }
            }
        }
        // Convert List<string> -> IEnumerable<string> for the OpenAPI model
        var securityRequirement = tempScopesByScheme.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
        );

        securitySchemes.Add(securityRequirement);
        return allSchemes;
    }
}
