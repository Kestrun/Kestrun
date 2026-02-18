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

    /// <summary>
    /// Ensures that appropriate client error responses (4XX) are included in the OpenApiOperation based on the presence of parameters, request body, content type validation, and response negotiation. If such responses are not already defined, it adds default 400, 406, 415, and/or 422 responses with a standard error schema.
    /// </summary>
    /// <param name="operation">The OpenApiOperation to modify.</param>
    /// <param name="meta">The OpenAPIPathMetadata containing metadata for the operation.</param>
    private void EnsureAutoClientErrorResponses(OpenApiOperation operation, OpenAPIPathMetadata meta)
    {
        operation.Responses ??= [];

        if (ResponseKeyExists(operation.Responses, "4XX") || ResponseKeyExists(operation.Responses, "default"))
        {
            return;
        }

        var statusesToAdd = GetAutoClientErrorStatuses(operation, meta);

        if (statusesToAdd.Count == 0)
        {
            return;
        }

        var errorSchemaId = EnsureAutoErrorSchemaComponent();
        var errorContentTypes = GetAutoErrorResponseContentTypes();

        AddMissingAutoClientErrorResponses(operation, statusesToAdd, errorSchemaId, errorContentTypes);
    }

    /// <summary>
    /// Determines which automatic client error statuses should be added for the operation.
    /// </summary>
    /// <param name="operation">The operation being generated.</param>
    /// <param name="meta">The metadata describing the route and request constraints.</param>
    /// <returns>A set of status codes to add.</returns>
    private static HashSet<string> GetAutoClientErrorStatuses(OpenApiOperation operation, OpenAPIPathMetadata meta)
    {
        var statusesToAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasParameters = meta.Parameters is { Count: > 0 };
        var hasRequestBody = meta.RequestBody is not null;
        var hasRequestContentTypeValidation = meta.MapOptions.AllowedRequestContentTypes.Count > 0;
        var responses = operation.Responses;
        var hasResponseNegotiation =
            meta.MapOptions.DefaultResponseContentType is { Count: > 0 } ||
            (responses is not null && responses.Values.Any(r => r.Content is { Count: > 0 }));

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

        return statusesToAdd;
    }

    /// <summary>
    /// Adds missing automatic client error responses to the OpenAPI operation.
    /// </summary>
    /// <param name="operation">The operation to modify.</param>
    /// <param name="statusesToAdd">The status codes to add when absent.</param>
    /// <param name="errorSchemaId">The schema id used for error response bodies.</param>
    /// <param name="errorContentTypes">The response content types for auto client errors.</param>
    private static void AddMissingAutoClientErrorResponses(
        OpenApiOperation operation,
        IReadOnlyCollection<string> statusesToAdd,
        string errorSchemaId,
        IReadOnlyList<string> errorContentTypes)
    {
        operation.Responses ??= [];
        var responses = operation.Responses;

        foreach (var status in statusesToAdd)
        {
            if (ResponseKeyExists(responses, status))
            {
                continue;
            }

            responses[status] = CreateAutoClientErrorResponse(status, errorSchemaId, errorContentTypes);
        }
    }

    /// <summary>
    /// Checks if a response key exists in the OpenApiResponses, ignoring case.
    /// </summary> <param name="responses">The OpenApiResponses to check.</param>
    /// <param name="statusCode">The status code key to look for.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    private static bool ResponseKeyExists(OpenApiResponses responses, string statusCode)
        => responses.Keys.Any(k => string.Equals(k, statusCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Ensures that a standard error response schema is defined in the OpenAPI document components. If the schema identified by AutoErrorResponseSchemaId (or a default ID if not set) does not already exist, it adds a new schema with properties for status, error, reason, timestamp, and optional details, exception, stackTrace, path, and
    /// </summary>
    /// <returns> The ID of the error schema component. </returns>
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
