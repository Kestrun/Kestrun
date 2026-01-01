using Microsoft.OpenApi;

namespace Kestrun.Callback;

/// <summary>
/// Represents a plan for executing a callback operation.
/// </summary>
/// <param name="CallbackId">The identifier for the callback</param>
/// <param name="UrlTemplate">The URL template for the callback</param>
/// <param name="Method"> The HTTP method for the callback</param>
/// <param name="OperationId"> The operation identifier for the callback</param>
/// <param name="PathParams"> The list of path parameters for the callback</param>
/// <param name="Body">The body plan for the callback</param>
public sealed record CallbackPlan(
    string CallbackId,                       // "paymentStatus"
    string UrlTemplate,                      // "{$request.body#/callbackUrls/status}/v1/payments/{paymentId}/status"
    HttpMethod Method,                       // POST
    string OperationId,                      // paymentStatusCallback__post__status
    IReadOnlyList<CallbackParamPlan> PathParams,
    CallbackBodyPlan? Body                  // schema info (ref) + media type
);

/// <summary>
/// Represents an execution plan for a callback, including resolved parameters.
/// </summary>
/// <param name="CallbackId">The identifier for the callback</param>
/// <param name="Plan">The callback plan</param>
/// <param name="BodyParameterName">The name of the body parameter, if any</param>
/// <param name="Parameters">The resolved parameters for the callback</param>
public sealed record CallBackExecutionPlan(
    string CallbackId,
    CallbackPlan Plan,
    string? BodyParameterName,
    Dictionary<string, object?> Parameters
);

/// <summary>
/// Represents a plan for a callback parameter.
/// </summary>
/// <param name="Name">The Parameter name</param>
/// <param name="Location">The location of the parameter (e.g., "path")</param>
public sealed record CallbackParamPlan(
    string Name,                             // "paymentId"
    string Location                         // "path" (keep string; or enum)

);

/// <summary>
/// Represents a plan for the body of a callback operation.
/// </summary>
/// <param name="MediaType">The media type of the callback body</param>
public sealed record CallbackBodyPlan(
    string MediaType                        // "application/json"
);
internal static class CallbackPlanCompiler
{
    internal static IReadOnlyList<CallbackPlan> Compile(OpenApiCallback callback, string callbackId)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(callbackId);

        var plans = new List<CallbackPlan>();

        if (callback.PathItems is null || callback.PathItems.Count == 0)
        {
            return plans;
        }

        foreach (var (expr, pathItemIntf) in callback.PathItems)
        {
            if (pathItemIntf is not OpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var (method, op) in pathItem.Operations)
            {
                if (op is null)
                {
                    continue;
                }

                var pathParams = ExtractPathParams(op);
                var bodyPlan = ExtractBodyPlan(op);

                plans.Add(new CallbackPlan(
                    CallbackId: callbackId,
                    UrlTemplate: expr.Expression,
                    Method: method,
                    OperationId: op.OperationId ?? $"{callbackId}__{method.Method.ToLowerInvariant()}",
                    PathParams: pathParams,
                    Body: bodyPlan
                ));
            }
        }

        return plans;
    }

    private static IReadOnlyList<CallbackParamPlan> ExtractPathParams(OpenApiOperation op)
    {
        return op.Parameters is null || op.Parameters.Count == 0
            ? []
            : [.. op.Parameters
            .Where(p => p is not null && string.Equals(p.In?.ToString(), "path", StringComparison.OrdinalIgnoreCase))
            .Select(p => new CallbackParamPlan(
                Name: p.Name ?? "",
                Location: "path"

            ))
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))];
    }

    private static CallbackBodyPlan? ExtractBodyPlan(OpenApiOperation op)
    {
        var rb = op.RequestBody;
        if (rb?.Content is null || rb.Content.Count == 0)
        {
            return null;
        }

        // Prefer application/json if present
        var kv = rb.Content.FirstOrDefault(c => string.Equals(c.Key, "application/json", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(kv.Key))
        {
            // otherwise take first
            kv = rb.Content.First();
        }

        var mediaType = kv.Key;
        return new CallbackBodyPlan(MediaType: mediaType);
    }
}
