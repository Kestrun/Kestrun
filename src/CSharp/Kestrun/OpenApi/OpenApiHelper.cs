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
    internal static List<string> AddSecurityRequirementObject(this KestrunHost host,
        string? scheme, List<string> policyList,
        List<Dictionary<string, List<string>>> securitySchemes)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(policyList);
        ArgumentNullException.ThrowIfNull(securitySchemes);

        var scopesByScheme = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var allSchemes = new HashSet<string>(StringComparer.Ordinal);

        AddExplicitScheme(scheme, scopesByScheme, allSchemes);
        MapPoliciesToSchemes(host, policyList, scopesByScheme, allSchemes);

        securitySchemes.Add(scopesByScheme);

        return [.. allSchemes];
    }

    /// <summary>
    /// Adds an explicit security scheme to the scopes dictionary and all schemes set.
    /// </summary>
    /// <param name="scheme">The security scheme name.</param>
    /// <param name="scopesByScheme">The dictionary mapping schemes to their scopes.</param>
    /// <param name="allSchemes">The set of all security schemes.</param>
    private static void AddExplicitScheme(
        string? scheme,
        Dictionary<string, List<string>> scopesByScheme,
        HashSet<string> allSchemes)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return;
        }

        _ = GetOrCreateScopeList(scopesByScheme, scheme);
        _ = allSchemes.Add(scheme);
    }
    /// <summary>
    /// Maps security policies to their corresponding security schemes.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="policyList">List of security policies.</param>
    /// <param name="scopesByScheme">The dictionary mapping schemes to their scopes.</param>
    /// <param name="allSchemes">The set of all security schemes.</param>
    private static void MapPoliciesToSchemes(
        KestrunHost host,
        IEnumerable<string> policyList,
        Dictionary<string, List<string>> scopesByScheme,
        HashSet<string> allSchemes)
    {
        foreach (var policy in policyList)
        {
            var schemesForPolicy = host.RegisteredAuthentications.GetSchemesByPolicy(policy);
            if (schemesForPolicy is null)
            {
                continue;
            }

            foreach (var schemeName in schemesForPolicy)
            {
                var scopeList = GetOrCreateScopeList(scopesByScheme, schemeName);

                if (!scopeList.Contains(policy))
                {
                    scopeList.Add(policy);
                }

                _ = allSchemes.Add(schemeName);
            }
        }
    }

    /// <summary>
    /// Retrieves or creates the scope list for a given security scheme.
    /// </summary>
    /// <param name="scopesByScheme">The dictionary mapping schemes to their scopes.</param>
    /// <param name="schemeName">The security scheme name.</param>
    /// <returns>The list of scopes associated with the security scheme.</returns>
    private static List<string> GetOrCreateScopeList(
        Dictionary<string, List<string>> scopesByScheme,
        string schemeName)
    {
        if (!scopesByScheme.TryGetValue(schemeName, out var scopeList))
        {
            scopeList = [];
            scopesByScheme[schemeName] = scopeList;
        }

        return scopeList;
    }
}
