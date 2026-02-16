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
        EnsureAutoClientErrorResponses(op, meta);
        ApplyExtensions(op, meta);
        return op;
    }

    private void EnsureAutoClientErrorResponses(OpenApiOperation operation, OpenAPIPathMetadata meta)
    {
        operation.Responses ??= [];

        if (ResponseKeyExists(operation.Responses, "4XX") || ResponseKeyExists(operation.Responses, "default"))
        {
            return;
        }

        var statusesToAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasParameters = meta.Parameters is { Count: > 0 };
        var hasRequestBody = meta.RequestBody is not null;
        var hasRequestContentTypeValidation = meta.MapOptions.AllowedRequestContentTypes.Count > 0;
        var hasResponseNegotiation =
            meta.MapOptions.DefaultResponseContentType is { Count: > 0 } ||
            operation.Responses.Values.Any(r => r.Content is { Count: > 0 });

        if (hasParameters || hasRequestBody)
        {
            _ = statusesToAdd.Add("400");
            _ = statusesToAdd.Add("422");
        }

        if (hasRequestBody || hasRequestContentTypeValidation)
        {
            _ = statusesToAdd.Add("415");
        }

        if (hasResponseNegotiation)
        {
            _ = statusesToAdd.Add("406");
        }

        if (statusesToAdd.Count == 0)
        {
            return;
        }

        var errorSchemaId = EnsureAutoErrorSchemaComponent();
        var errorContentTypes = GetAutoErrorResponseContentTypes();

        foreach (var status in statusesToAdd)
        {
            if (ResponseKeyExists(operation.Responses, status))
            {
                continue;
            }

            operation.Responses[status] = CreateAutoClientErrorResponse(status, errorSchemaId, errorContentTypes);
        }
    }

    private static bool ResponseKeyExists(OpenApiResponses responses, string statusCode)
        => responses.Keys.Any(k => string.Equals(k, statusCode, StringComparison.OrdinalIgnoreCase));

    private string EnsureAutoErrorSchemaComponent()
    {
        Document.Components ??= new OpenApiComponents();
        Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        var autoSchemaId = string.IsNullOrWhiteSpace(AutoErrorResponseSchemaId)
            ? DefaultAutoErrorResponseSchemaId
            : AutoErrorResponseSchemaId;

        if (!Document.Components.Schemas.ContainsKey(autoSchemaId))
        {
            Document.Components.Schemas[autoSchemaId] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string>(StringComparer.Ordinal) { "status", "error", "reason", "timestamp" },
                Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                {
                    ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                    ["error"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["reason"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["timestamp"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
                    ["details"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["exception"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["stackTrace"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["path"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["method"] = new OpenApiSchema { Type = JsonSchemaType.String },
                }
            };
        }

        return autoSchemaId;
    }

    private IReadOnlyList<string> GetAutoErrorResponseContentTypes()
    {
        if (AutoErrorResponseContentTypes is null || AutoErrorResponseContentTypes.Length == 0)
        {
            return [DefaultAutoErrorResponseContentType];
        }

        var contentTypes = AutoErrorResponseContentTypes
            .Where(ct => !string.IsNullOrWhiteSpace(ct))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return contentTypes.Length == 0
            ? [DefaultAutoErrorResponseContentType]
            : contentTypes;
    }

    private static OpenApiResponse CreateAutoClientErrorResponse(string statusCode, string errorSchemaId, IReadOnlyList<string> contentTypes)
    {
        var description = statusCode switch
        {
            "400" => "Bad Request",
            "406" => "Not Acceptable",
            "415" => "Unsupported Media Type",
            "422" => "Unprocessable Entity",
            _ => "Client Error"
        };

        var content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        foreach (var contentType in contentTypes)
        {
            content[contentType] = new OpenApiMediaType
            {
                Schema = new OpenApiSchemaReference(errorSchemaId)
            };
        }

        return new OpenApiResponse
        {
            Description = description,
            Content = content
        };
    }
    /// <summary>
    /// Applies extension information from metadata to the OpenApiOperation.
    /// </summary>
    /// <param name="op">The OpenApiOperation to modify.</param>
    /// <param name="meta">The OpenAPIPathMetadata containing extension information.</param>
    private static void ApplyExtensions(OpenApiOperation op, OpenAPIPathMetadata meta)
    {
        if (meta.Extensions is null || meta.Extensions.Count == 0)
        {
            return;
        }
        op.Extensions = meta.Extensions;
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
