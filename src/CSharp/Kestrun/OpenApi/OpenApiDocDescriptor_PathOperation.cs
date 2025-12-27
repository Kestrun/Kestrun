using Kestrun.Hosting.Options;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for accessing OpenAPI document components.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Builds an OpenApiOperation from OpenAPIPathMetadata.
    /// </summary>
    /// <param name="meta">The OpenAPIPathMetadata to build from.</param>
    /// <returns>The constructed OpenApiOperation.</returns>
    private OpenApiOperation BuildOperationFromMetadata(OpenAPIPathMetadata meta)
    {
        var op = new OpenApiOperation
        {
            OperationId = string.IsNullOrWhiteSpace(meta.OperationId) ? null : meta.OperationId,
            Summary = string.IsNullOrWhiteSpace(meta.Summary) ? null : meta.Summary,
            Description = string.IsNullOrWhiteSpace(meta.Description) ? null : meta.Description,
            Deprecated = meta.Deprecated,
            ExternalDocs = meta.ExternalDocs,
            RequestBody = meta.RequestBody,
            Responses = meta.Responses ?? new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "Success" } }
        };

        ApplyTags(op, meta);
        ApplyServers(op, meta);
        ApplyParameters(op, meta);
        ApplyCallbacks(op, meta);
        ApplySecurity(op, meta);

        return op;
    }

    /// <summary>
    /// Applies tags from metadata to the OpenApiOperation.
    /// </summary>
    /// <param name="op">The OpenApiOperation to modify.</param>
    /// <param name="meta">The OpenAPIPathMetadata containing tags.</param>
    private static void ApplyTags(OpenApiOperation op, OpenAPIPathMetadata meta)
    {
        if (meta.Tags.Count > 0)
        {
            op.Tags = new HashSet<OpenApiTagReference>();
            foreach (var t in meta.Tags ?? [])
            {
                _ = op.Tags.Add(new OpenApiTagReference(t));
            }
        }
    }

    /// <summary>
    /// Applies server information from metadata to the OpenApiOperation.
    /// </summary>
    /// <param name="op">The OpenApiOperation to modify.</param>
    /// <param name="meta">The OpenAPIPathMetadata containing server information.</param>
    private void ApplyServers(OpenApiOperation op, OpenAPIPathMetadata meta)
    {
        try
        {
            if (meta.Servers is { Count: > 0 })
            {
                dynamic d = op;
                if (d.Servers == null) { d.Servers = new List<OpenApiServer>(); }
                foreach (var s in meta.Servers) { d.Servers.Add(s); }
            }
        }
        catch (Exception ex)
        {
            Host.Logger.Warning(ex, "Failed to set operation-level servers for OpenAPI operation {OperationId}", op.OperationId);
        }
    }

    /// <summary>
    /// Applies parameter information from metadata to the OpenApiOperation.
    /// </summary>
    /// <param name="op">The OpenApiOperation to modify.</param>
    /// <param name="meta">The OpenAPIPathMetadata containing parameter information.</param>
    private void ApplyParameters(OpenApiOperation op, OpenAPIPathMetadata meta)
    {
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
    }

    /// <summary>
    /// Applies security requirement information from metadata to the OpenApiOperation.
    /// </summary>
    /// <param name="op">The OpenApiOperation to modify.</param>
    /// <param name="meta">The OpenAPIPathMetadata containing security requirement information.</param>
    private void ApplySecurity(OpenApiOperation op, OpenAPIPathMetadata meta)
    {
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
    }
}
