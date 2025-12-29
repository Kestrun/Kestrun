using Kestrun.Models;
using System.Text.RegularExpressions;
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
    /// <param name="plan">The callback plan to create a request from.</param>
    /// <param name="ctx">The callback runtime context providing values for tokens and JSON data.</param>
    /// <param name="urlResolver">The URL resolver to resolve callback URLs.</param>
    /// <param name="bodySerializer">The body serializer to serialize callback request bodies.</param>
    /// <param name="options">The callback dispatch options.</param>
    /// <returns>A list of created <see cref="CallbackRequest"/> instances.</returns>
    public static CallbackRequest FromPlan(
        CallbackPlan plan,
        KestrunContext ctx,
        ICallbackUrlResolver urlResolver,
        ICallbackBodySerializer bodySerializer,
        CallbackDispatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(urlResolver);
        ArgumentNullException.ThrowIfNull(bodySerializer);
        ArgumentNullException.ThrowIfNull(options);

        var correlationId = ctx.TraceIdentifier;

        // 1) Extract placeholder names from the template
        var templateParamNames = ExtractTemplateParams(plan.UrlTemplate);

        // 2) Build a stable seed based on those placeholders + resolved values
        // (sorted so ordering in template doesn't change the key)
        var seedParts = templateParamNames
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n =>
            {
                if (!ctx.Parameters.Parameters.TryGetValue(n, out var resolved))
                {
                    return null;
                }

                var s = resolved.Value?.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : $"{n}={s}";
            })
            .Where(x => x is not null)
            .ToArray();

        var idSeed = seedParts.Length > 0
            ? string.Join("&", seedParts)
            : correlationId;

        var idempotencyKey = $"{idSeed}:{plan.CallbackId}:{plan.OperationId}";

        var rt = CallbackRuntimeContextFactory.FromHttpContext(ctx);
        var targetUrl = urlResolver.Resolve(plan.UrlTemplate, rt);

        var (contentType, body) = bodySerializer.Serialize(plan, rt);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Correlation-Id"] = correlationId,
            ["Idempotency-Key"] = idempotencyKey,
            ["X-Kestrun-CallbackId"] = plan.CallbackId
        };

        return new CallbackRequest(
            callbackId: plan.CallbackId,
            operationId: plan.OperationId,
            targetUrl: targetUrl,
            httpMethod: plan.Method.Method.ToUpperInvariant(),
            headers: headers,
            contentType: contentType,
            body: body,
            correlationId: correlationId,
            idempotencyKey: idempotencyKey,
            timeout: options.DefaultTimeout,
            signatureKeyId: options.SignatureKeyId
        );
    }

    [GeneratedRegex(@"\{(?<name>[^{}:/\?]+)(?:[:][^{}]+)?\}", RegexOptions.Compiled)]
    private static partial Regex TemplateParameterRegex();
}
