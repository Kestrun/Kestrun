using Microsoft.OpenApi;
using Kestrun.Hosting.Options;
using Kestrun.Utilities;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Populates Document.Paths from the registered routes using OpenAPI metadata on each route.
    /// </summary>
    /// <param name="routes">The registered routes with OpenAPI metadata.</param>
    private void BuildPathsFromRegisteredRoutes(Dictionary<(string Pattern, HttpVerb Method), MapRouteOptions> routes)
    {
        if (routes is null || routes.Count == 0)
        {
            return;
        }
        Document.Paths = [];

        var groups = routes
            .GroupBy(kvp => kvp.Key.Pattern, StringComparer.Ordinal)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Where(g => g.Any(kvp => kvp.Value?.OpenAPI?.Count > 0));

        foreach (var grp in groups)
        {
            ProcessRouteGroup(grp);
        }
    }

    /// <summary>
    /// Processes a group of routes sharing the same pattern to build the corresponding OpenAPI path item.
    /// </summary>
    /// <param name="grp">The group of routes sharing the same pattern. </param>
    private void ProcessRouteGroup(IGrouping<string, KeyValuePair<(string Pattern, HttpVerb Method), MapRouteOptions>> grp)
    {
        var pattern = grp.Key;
        var pathItem = GetOrCreatePathItem(pattern);
        OpenAPICommonMetadata? pathMeta = null;

        foreach (var kvp in grp)
        {
            pathMeta = ProcessRouteOperation(kvp, pathItem, pathMeta);
        }

        if (pathMeta is not null)
        {
            ApplyPathLevelMetadata(pathItem, pathMeta, pattern);
        }
    }
    /// <summary>
    /// Retrieves or creates an OpenApiPathItem for the specified pattern.
    /// </summary>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The corresponding OpenApiPathItem.</returns>
    private OpenApiPathItem GetOrCreatePathItem(string pattern)
    {
        Document.Paths ??= [];
        if (!Document.Paths.TryGetValue(pattern, out var pathInterface) || pathInterface is null)
        {
            pathInterface = new OpenApiPathItem();
            Document.Paths[pattern] = pathInterface;
        }
        return (OpenApiPathItem)pathInterface;
    }

    /// <summary>
    /// Processes a single route operation and adds it to the OpenApiPathItem.
    /// </summary>
    /// <param name="kvp">The key-value pair representing the route pattern, HTTP method, and route options.</param>
    /// <param name="pathItem">The OpenApiPathItem to which the operation will be added.</param>
    /// <param name="currentPathMeta">The current path-level OpenAPI metadata.</param>
    /// <returns>The updated path-level OpenAPI metadata.</returns>
    private OpenAPICommonMetadata? ProcessRouteOperation(KeyValuePair<(string Pattern, HttpVerb Method), MapRouteOptions> kvp, OpenApiPathItem pathItem, OpenAPICommonMetadata? currentPathMeta)
    {
        var method = kvp.Key.Method;
        var map = kvp.Value;

        if (map is null || map.OpenAPI.Count == 0)
        {
            return currentPathMeta;
        }

        if ((map.PathLevelOpenAPIMetadata is not null) && (currentPathMeta is null))
        {
            currentPathMeta = map.PathLevelOpenAPIMetadata;
        }

        if (map.OpenAPI.TryGetValue(method, out var meta) && meta.Enabled)
        {
            if (meta.DocumentId is not null && !meta.DocumentId.Contains(DocumentId))
            {
                return currentPathMeta;
            }
            var op = BuildOperationFromMetadata(meta);
            pathItem.AddOperation(HttpMethod.Parse(method.ToMethodString()), op);
        }

        return currentPathMeta;
    }

    /// <summary>
    /// Applies path-level OpenAPI metadata to the given OpenApiPathItem.
    /// </summary>
    /// <param name="pathItem">The OpenApiPathItem to which the metadata will be applied.</param>
    /// <param name="pathMeta">The path-level OpenAPI metadata.</param>
    /// <param name="pattern">The route pattern associated with the path item.</param>
    private void ApplyPathLevelMetadata(OpenApiPathItem pathItem, OpenAPICommonMetadata pathMeta, string pattern)
    {
        pathItem.Description = pathMeta.Description;
        pathItem.Summary = pathMeta.Summary;
        try
        {
            ApplyPathLevelServers(pathItem, pathMeta);
            ApplyPathLevelParameters(pathItem, pathMeta);
        }
        catch (Exception ex)
        {
            if (Host.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                Host.Logger.Debug(ex, "Tolerated exception in path-level OpenAPI metadata assignment for pattern {Pattern}", pattern);
            }
        }
    }

    /// <summary>
    /// Applies server information from path-level metadata to the OpenApiPathItem.
    /// </summary>
    /// <param name="pathItem">The OpenApiPathItem to modify.</param>
    /// <param name="pathMeta">The path-level OpenAPI metadata containing server information.</param>
    private static void ApplyPathLevelServers(OpenApiPathItem pathItem, OpenAPICommonMetadata pathMeta)
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
    }

    /// <summary>
    /// Applies parameter information from path-level metadata to the OpenApiPathItem.
    /// </summary>
    /// <param name="pathItem">The OpenApiPathItem to modify.</param>
    /// <param name="pathMeta">The path-level OpenAPI metadata containing parameter information.</param>
    private static void ApplyPathLevelParameters(OpenApiPathItem pathItem, OpenAPICommonMetadata pathMeta)
    {
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
}
