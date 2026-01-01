using Kestrun.Models;
using System.Text.RegularExpressions;
using Serilog.Events;
namespace Kestrun.Callback;

/// <summary>
/// Factory for creating <see cref="CallbackRequest"/> instances from callback plans and runtime context.
/// </summary>
public static partial class CallbackRequestFactory
{
    private static readonly Regex TemplateParamRegex =
        TemplateParameterRegex();
    // - captures {id} and also {id:int} style constraints (keeps "id")

    private static HashSet<string> ExtractTemplateParams(string urlTemplate)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            return names;
        }

        foreach (Match m in TemplateParamRegex.Matches(urlTemplate))
        {
            var name = m.Groups["name"].Value.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                _ = names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Creates a list of <see cref="CallbackRequest"/> instances from the given callback plans and runtime context.
    /// </summary>
    /// <param name="executionPlan">The callback plan to create a request from.</param>
    /// <param name="ctx">The callback runtime context providing values for tokens and JSON data.</param>
    /// <param name="urlResolver">The URL resolver to resolve callback URLs.</param>
    /// <param name="bodySerializer">The body serializer to serialize callback request bodies.</param>
    /// <param name="options">The callback dispatch options.</param>
    /// <returns>A list of created <see cref="CallbackRequest"/> instances.</returns>
    public static CallbackRequest FromPlan(
        CallBackExecutionPlan executionPlan,
        KestrunContext ctx,
        ICallbackUrlResolver urlResolver,
        ICallbackBodySerializer bodySerializer,
        CallbackDispatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(executionPlan);
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(urlResolver);
        ArgumentNullException.ThrowIfNull(bodySerializer);
        ArgumentNullException.ThrowIfNull(options);

        var correlationId = ctx.TraceIdentifier;
        var plan = executionPlan.Plan;
        // Build callback runtime context:
        // - Start with request-derived vars/body (for {$request.body#/...} runtime expressions)
        // - Overlay callback execution-plan parameters (for {token} placeholders like {paymentId})
        var requestRt = CallbackRuntimeContextFactory.FromHttpContext(ctx);
        var mergedVars = new Dictionary<string, object?>(requestRt.Vars, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in executionPlan.Parameters)
        {
            mergedVars[name] = value;
        }

        var rt = requestRt with { Vars = mergedVars };

        // 1) Extract placeholder names from the template
        var templateParamNames = ExtractTemplateParams(plan.UrlTemplate);

        // 2) Build a stable seed based on those placeholders + resolved values
        // (sorted so ordering in template doesn't change the key)
        var seedParts = templateParamNames
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n =>
            {
                if (!mergedVars.TryGetValue(n, out var resolved) || resolved is null)
                {
                    return null;
                }

                var s = resolved.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : $"{n}={s}";
            })
            .Where(x => x is not null)
            .ToArray();

        var idSeed = seedParts.Length > 0
            ? string.Join("&", seedParts)
            : correlationId;

        var idempotencyKey = $"{idSeed}:{plan.CallbackId}:{plan.OperationId}";

        var targetUrl = urlResolver.Resolve(plan.UrlTemplate, rt);

        var (contentType, body) = bodySerializer.Serialize(plan, rt);
        //Todo: Add option to override content type?
        //Todo: Add custom headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Correlation-Id"] = correlationId,
            ["Idempotency-Key"] = idempotencyKey,
            ["X-Kestrun-CallbackId"] = plan.CallbackId
        };
        // Add body
        var bodyBytes = (executionPlan.BodyParameterName == null) ?
            null :
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(executionPlan.Parameters[executionPlan.BodyParameterName]);
        if (ctx.Logger.IsEnabled(LogEventLevel.Debug))
        {
            ctx.Logger.Debug("Created CallbackRequest: CallbackId={CallbackId}, OperationId={OperationId}, TargetUrl={TargetUrl}, HttpMethod={HttpMethod}, ContentType={ContentType}, BodyLength={BodyLength}, CorrelationId={CorrelationId}, IdempotencyKey={IdempotencyKey}",
                plan.CallbackId,
                plan.OperationId,
                targetUrl,
                plan.Method.Method.ToUpperInvariant(),
                contentType,
                bodyBytes?.Length ?? 0,
                correlationId,
                idempotencyKey);

            ctx.Logger.Debug("CallbackRequest Headers: {Headers}", headers);
            ctx.Logger.Debug("CallbackRequest Body: {Body}", bodyBytes is null ? "<null>" : System.Text.Encoding.UTF8.GetString(bodyBytes));
        }
        // Create CallbackRequest
        return new CallbackRequest(
            callbackId: plan.CallbackId,
            operationId: plan.OperationId,
            targetUrl: targetUrl,
            httpMethod: plan.Method.Method.ToUpperInvariant(),
            headers: headers,
            contentType: contentType,
            body: bodyBytes,
            correlationId: correlationId,
            idempotencyKey: idempotencyKey,
            timeout: options.DefaultTimeout
        );
    }

    [GeneratedRegex(@"\{(?<name>[^{}:/\?]+)(?:[:][^{}]+)?\}", RegexOptions.Compiled)]
    private static partial Regex TemplateParameterRegex();
}
