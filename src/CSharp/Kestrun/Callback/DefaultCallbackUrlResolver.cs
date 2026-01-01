using System.Text.RegularExpressions;
using System.Text.Json;
namespace Kestrun.Callback;

/// <summary>
/// Default implementation of <see cref="ICallbackUrlResolver"/> that resolves callback URLs
/// using JSON Pointer expressions and variable tokens.
/// </summary>
public sealed partial class DefaultCallbackUrlResolver : ICallbackUrlResolver
{
    private static readonly Regex RuntimeExpr = GeneratedRuntimeRegex();

    private static readonly Regex Token = GeneratedTokenRegex();

    /// <summary>
    /// Resolves the given URL template into a full URI using the provided callback runtime context.
    /// </summary>
    /// <param name="urlTemplate">The URL template containing tokens and JSON Pointer expressions.</param>
    /// <param name="ctx">The callback runtime context providing values for tokens and JSON data.</param>
    /// <returns>A fully resolved URI based on the template and context.</returns>
    /// <exception cref="ArgumentException">Thrown when the urlTemplate is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the URL template cannot be resolved to a valid URI.</exception>
    public Uri Resolve(string urlTemplate, CallbackRuntimeContext ctx)
    {
        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            throw new ArgumentException("urlTemplate is empty.", nameof(urlTemplate));
        }

        ArgumentNullException.ThrowIfNull(ctx);

        // 1) Resolve {$request.body#/json/pointer} using the typed body
        var s = RuntimeExpr.Replace(urlTemplate, m =>
        {
            var ptr = m.Groups["ptr"].Value; // like "/callbackUrls/status"

            if (ctx.CallbackPayload is null)
            {
                throw new InvalidOperationException(
                    $"Callback url uses request.body pointer '{ptr}' but the request body is null.");
            }

            // Convert typed object -> JsonElement (on demand)
            JsonElement root;
            try
            {
                root = JsonSerializer.SerializeToElement(ctx.CallbackPayload);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize request body for evaluating pointer '{ptr}'.", ex);
            }

            var value = ResolveJsonPointer(root, ptr);

            // Runtime expressions are inserted as text; strings are inserted raw,
            // non-strings use their JSON textual form.
            return value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? "")
                : value.GetRawText();
        });

        // 2) Replace {token} placeholders from Vars
        s = Token.Replace(s, m =>
        {
            var name = m.Groups["name"].Value;

            return !ctx.Vars.TryGetValue(name, out var v) || v is null
                ? throw new InvalidOperationException(
                    $"Callback url requires token '{name}' but it was not found in runtime Vars.")
                : Uri.EscapeDataString(v.ToString()!);
        });

        // 3) Make Uri
        if (Uri.TryCreate(s, UriKind.Absolute, out var abs))
        {
            // On Unix, a leading-slash path like "/v1/foo" parses as an absolute file URI (file:///v1/foo).
            // Callback URLs are expected to be HTTP(S). Treat file URIs from leading-slash inputs as relative.
            if (!(abs.Scheme == Uri.UriSchemeFile && s.StartsWith('/', StringComparison.Ordinal)))
            {
                return abs;
            }
        }
        // Relative Uri: combine with DefaultBaseUri
        return ctx.DefaultBaseUri is null
            ? throw new InvalidOperationException(
                $"Callback url resolved to '{s}' (not absolute) and DefaultBaseUri is null.")
            : new Uri(ctx.DefaultBaseUri, s);
    }

    // Minimal JSON Pointer resolver (RFC 6901-ish)
    private static JsonElement ResolveJsonPointer(JsonElement root, string pointer)
    {
        if (pointer is "" or "/")
        {
            return root;
        }

        if (!pointer.StartsWith('/'))
        {
            throw new FormatException($"Invalid JSON pointer '{pointer}'.");
        }

        var current = root;
        var segments = pointer.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in segments)
        {
            var seg = raw.Replace("~1", "/").Replace("~0", "~");

            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(seg, out current))
                {
                    throw new KeyNotFoundException($"JSON pointer segment '{seg}' not found.");
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(seg, out var idx))
                {
                    throw new FormatException($"JSON pointer segment '{seg}' is not a valid array index.");
                }

                if (idx < 0 || idx >= current.GetArrayLength())
                {
                    throw new IndexOutOfRangeException($"JSON pointer index {idx} out of range.");
                }

                current = current[idx];
            }
            else
            {
                throw new InvalidOperationException($"Cannot traverse JSON pointer through {current.ValueKind}.");
            }
        }

        return current;
    }

    [GeneratedRegex(@"\{\$request\.body#(?<ptr>\/[^}]*)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedRuntimeRegex();

    [GeneratedRegex(@"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedTokenRegex();
}
