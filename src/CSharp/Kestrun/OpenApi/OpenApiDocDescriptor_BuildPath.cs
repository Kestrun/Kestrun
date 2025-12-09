using Microsoft.OpenApi;
using Kestrun.Hosting.Options;
using Kestrun.Utilities;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Populates Document.Paths from the registered routes using OpenAPI metadata on each route.
    /// </summary>
    private void BuildPathsFromRegisteredRoutes(IDictionary<(string Pattern, HttpVerb Method), MapRouteOptions> routes)
    {
        if (routes is null || routes.Count == 0)
        {
            return;
        }
        Document.Paths = [];
        // Group by path pattern
        foreach (var grp in routes
            .GroupBy(kvp => kvp.Key.Pattern, StringComparer.Ordinal)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Where(g => g.Any(kvp => kvp.Value?.OpenAPI?.Count > 0)))
        {
            OpenAPICommonMetadata? pathMeta = null;
            var pattern = grp.Key;

            // Ensure a PathItem exists
            Document.Paths ??= [];

            if (!Document.Paths.TryGetValue(pattern, out var pathInterface) || pathInterface is null)
            {
                pathInterface = new OpenApiPathItem();
                Document.Paths[pattern] = pathInterface;
            }
            // Populate operations
            var pathItem = (OpenApiPathItem)pathInterface;
            foreach (var kvp in grp)
            {
                var method = kvp.Key.Method;
                var map = kvp.Value;
                if (map is null || map.OpenAPI.Count == 0)
                {
                    continue;
                }

                if ((map.PathLevelOpenAPIMetadata is not null) && (pathMeta is null))
                {
                    pathMeta = map.PathLevelOpenAPIMetadata;
                }

                // Decide whether to include the operation. Prefer explicit enable, but also include when metadata is present.
                if (!map.OpenAPI.TryGetValue(method, out var meta) || (!meta.Enabled))
                {
                    // Skip silent routes by default
                    continue;
                }

                var op = BuildOperationFromMetadata(meta);

                pathItem.AddOperation(HttpMethod.Parse(method.ToMethodString()), op);
            }
            // Optionally apply servers/parameters at the path level for quick discovery in PS views
            if (pathMeta is not null)
            {
                pathItem.Description = pathMeta.Description;
                pathItem.Summary = pathMeta.Summary;
                try
                {
                    if (pathMeta.Servers is { Count: > 0 })
                    {
                        dynamic dPath = pathItem;
                        if (dPath.Servers == null) { dPath.Servers = new List<OpenApiServer>(); }
                        foreach (var s in pathMeta.Servers)
                        {
                            dPath.Servers.Add(s);
                        }
                    }
                    if (pathMeta.Parameters is { Count: > 0 })
                    {
                        dynamic dPath = pathItem;
                        if (dPath.Parameters == null) { dPath.Parameters = new List<IOpenApiParameter>(); }
                        foreach (var p in pathMeta.Parameters)
                        {
                            dPath.Parameters.Add(p);
                        }
                    }
                }
                catch { /* tolerate differing model shapes */ }
            }
        }
    }

    /// <summary>
    /// Builds an OpenApiOperation from OpenAPIMetadata.
    /// </summary>
    /// <param name="meta">The OpenAPIMetadata to build from.</param>
    /// <returns>The constructed OpenApiOperation.</returns>
    private OpenApiOperation BuildOperationFromMetadata(OpenAPIMetadata meta)
    {
        var op = new OpenApiOperation
        {
            OperationId = string.IsNullOrWhiteSpace(meta.OperationId) ? null : meta.OperationId,

            Summary = string.IsNullOrWhiteSpace(meta.Summary) ? null : meta.Summary,
            Description = string.IsNullOrWhiteSpace(meta.Description) ? null : meta.Description,
            Deprecated = meta.Deprecated
        };
        // Tags
        if (meta.Tags.Count > 0)
        {
            op.Tags = new HashSet<OpenApiTagReference>();
            foreach (var t in meta.Tags ?? [])
            {
                _ = op.Tags.Add(new OpenApiTagReference(t));
            }
        }
        // External docs
        if (meta.ExternalDocs is not null)
        {
            op.ExternalDocs = meta.ExternalDocs;
        }

        // Servers (operation-level)
        try
        {
            if (meta.Servers is { Count: > 0 })
            {
                dynamic d = op;
                if (d.Servers == null) { d.Servers = new List<OpenApiServer>(); }
                foreach (var s in meta.Servers) { d.Servers.Add(s); }
            }
        }
        catch { Host.Logger?.Warning("Failed to set operation-level servers for OpenAPI operation {OperationId}", op.OperationId); }

        // Parameters (operation-level)
        try
        {
            if (meta.Parameters is { Count: > 0 })
            {
                dynamic d = op;
                if (d.Parameters == null) { d.Parameters = new List<IOpenApiParameter>(); }
                foreach (var p in meta.Parameters) { d.Parameters.Add(p); }
            }
        }
        catch { Host.Logger?.Warning("Failed to set operation-level parameters for OpenAPI operation {OperationId}", op.OperationId); }

        // Request body
        if (meta.RequestBody is not null)
        {
            op.RequestBody = meta.RequestBody;
        }

        // Responses (required by spec)
        op.Responses = meta.Responses ?? new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "Success" } };
        // Callbacks
        if (meta.Callbacks is not null && meta.Callbacks.Count > 0)
        {
            op.Callbacks = new Dictionary<string, IOpenApiCallback>(meta.Callbacks);
        }
        if (meta.SecuritySchemes is not null && meta.SecuritySchemes.Count != 0)
        {
            op.Security ??= [];

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var schemeName in meta.SecuritySchemes
                         .SelectMany(d => d.Keys)
                         .Distinct())
            {
                if (!seen.Add(schemeName))
                {
                    continue;
                }
                // Gather scopes for this scheme
                var scopesForScheme = meta.SecuritySchemes
                    .SelectMany(dict => dict)
                    .Where(kv => kv.Key == schemeName)
                    .SelectMany(kv => kv.Value)
                    .Distinct()
                    .ToList();
                // Build requirement
                var requirement = new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(schemeName, Document)] = scopesForScheme
                };

                op.Security.Add(requirement);
            }
        }
        else if (meta.SecuritySchemes is not null && meta.SecuritySchemes.Count == 0)
        {
            // Explicitly anonymous for this operation (overrides Document.Security)
            op.Security = [];
        }

        return op;
    }
}
