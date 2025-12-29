using System.Text.Json;
using System.Text.RegularExpressions;
using Kestrun.Models;

namespace Kestrun.Callback;

/// <summary>
/// Factory for creating <see cref="CallbackRuntimeContext"/> instances from HTTP context.
/// </summary>
public static partial class CallbackRuntimeContextFactory
{
    // Matches {id} and {id:int} etc; ignores {$request.body#/...} because of / and #
    private static readonly Regex TemplateParamRegex =
        TemplateParameterRegex();

    private static HashSet<string> ExtractTemplateParams(string urlTemplate)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            return set;
        }

        foreach (Match m in TemplateParamRegex.Matches(urlTemplate))
        {
            var name = m.Groups["name"].Value.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                _ = set.Add(name);
            }
        }

        return set;
    }
    /// <summary>
    /// Creates a <see cref="CallbackRuntimeContext"/> from the given <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="ctx">The HTTP context from which to create the callback runtime context.</param>
    /// <param name="urlTemplate">An optional URL template to extract template parameters for idempotency key generation.</param>
    /// <returns>A new instance of <see cref="CallbackRuntimeContext"/> populated with data from the HTTP context.</returns>
    public static CallbackRuntimeContext FromHttpContext(KestrunContext ctx, string? urlTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var correlationId = ctx.TraceIdentifier;

        // Vars come from resolved OpenAPI parameters (this is the key fix)
        var vars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in ctx.Parameters.Parameters)
        {
            vars[kv.Key] = kv.Value.Value;
        }

        // Typed body is already resolved
        var requestBody = ctx.Parameters.Body?.Value;

        // Idempotency seed: derived from placeholders in the callback URL template (if provided)
        string idempotencySeed;
        if (!string.IsNullOrWhiteSpace(urlTemplate))
        {
            var names = ExtractTemplateParams(urlTemplate);

            var seedParts = names
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n =>
                {
                    if (!vars.TryGetValue(n, out var v) || v is null)
                    {
                        return null;
                    }

                    var s = v.ToString();
                    return string.IsNullOrWhiteSpace(s) ? null : $"{n}={s}";
                })
                .Where(x => x is not null)
                .ToArray();

            idempotencySeed = seedParts.Length > 0
                ? string.Join("&", seedParts)
                : correlationId;
        }
        else
        {
            // No template => don't guess which param matters
            idempotencySeed = correlationId;
        }

        return new CallbackRuntimeContext(
            CorrelationId: correlationId,
            IdempotencyKeySeed: idempotencySeed,
            DefaultBaseUri: null,
            Vars: vars,
            CallbackPayload: requestBody    // <-- typed body goes here
        );
    }

    [GeneratedRegex(@"\{(?<name>[^{}:/\?]+)(?:[:][^{}]+)?\}", RegexOptions.Compiled)]
    private static partial Regex TemplateParameterRegex();
}

