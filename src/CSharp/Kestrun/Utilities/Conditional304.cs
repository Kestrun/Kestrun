using System.Security.Cryptography;
using System.Text;
using Microsoft.Net.Http.Headers;

namespace Kestrun.Utilities;

/// <summary>
/// Helper for writing conditional 304 Not Modified responses based on ETag and Last-Modified headers.
/// </summary>
internal static class CacheRevalidation
{
    /// <summary>
    /// Returns true if a 304 Not Modified was written. Otherwise sets validators on the response and returns false.
    /// Does NOT write a body on miss; the caller should write the payload/status.
    /// </summary>
    /// <param name="ctx">The current HTTP context.</param>
    /// <param name="payload">The response payload, used to derive an ETag if none is provided. Can be a byte[], ReadOnlyMemory&lt;byte&gt;, Memory&lt;byte&gt;, ArraySegment&lt;byte&gt;, Stream, IFormFile, or string. If a string, the charset is derived from the Accept-Charset header (default UTF-8).</param>
    /// <param name="etag">An optional ETag to use. If not provided, an ETag is derived from the payload if possible. Quotes are optional.</param>
    /// <param name="weakETag">If true, the provided or derived ETag is marked as weak (prefixed with W/).</param>
    /// <param name="lastModified">An optional Last-Modified timestamp to use.</param>
    /// <returns>True if a 304 Not Modified was written; otherwise false.</returns>
    public static bool TryWrite304(
       HttpContext ctx,
       object? payload,        // for string payloads; use to set charset on your response
       string? etag = null,                   // quotes optional; if provided, no hashing is done
       bool weakETag = false,
       DateTimeOffset? lastModified = null)
    {
        // Only relevant for safe methods
        var req = ctx.Request;
        var resp = ctx.Response;
        var isSafe = HttpMethods.IsGet(req.Method) || HttpMethods.IsHead(req.Method);

        // Normalize/provide ETag
        var normalizedETag = NormalizeETag(etag);

        // If no explicit ETag, derive from payload
        byte[]? toHash = null;
        if (normalizedETag is null && payload is not null)
        {
            switch (payload)
            {
                case byte[] b:
                    toHash = b;
                    break;

                case ReadOnlyMemory<byte> rom:
                    toHash = rom.ToArray();
                    break;

                case Memory<byte> mem:
                    toHash = mem.ToArray();
                    break;

                case ArraySegment<byte> seg:
                    toHash = seg.Array is null ? [] : seg.Array.AsSpan(seg.Offset, seg.Count).ToArray();
                    break;

                case string text:
                    var chosenEncoding = ChooseEncodingFromAcceptCharset(req.Headers[HeaderNames.AcceptCharset]);
                    toHash = chosenEncoding.GetBytes(text);
                    break;

                case Stream s:
                    toHash = ReadAllBytesPreservePosition(s);
                    break;

                case IFormFile formFile:
                    using (var fs = formFile.OpenReadStream())
                    {
                        toHash = ReadAllBytesPreservePosition(fs);
                    }

                    break;

                default:
                    // no implicit object -> bytes conversion; require an explicit ETag or byte-like payload
                    throw new ArgumentException(
                        $"Cannot derive bytes from payload of type '{payload.GetType().FullName}'. " +
                        "Provide an explicit ETag or pass a byte-like payload (byte[], ReadOnlyMemory<byte>, Memory<byte>, ArraySegment<byte>, Stream, IFormFile, or string).");
            }

            normalizedETag = ComputeETagFromBytes(toHash, weakETag: false); // compute strong by default
        }

        if (weakETag && normalizedETag is not null && !normalizedETag.StartsWith("W/", StringComparison.Ordinal))
        {
            normalizedETag = "W/" + normalizedETag;
        }

        // ---------- Conditional check: ETag precedence ----------
        if (isSafe && normalizedETag is string etagValue &&
            req.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm) &&
            inm.Any(v => !string.IsNullOrEmpty(v) &&
                         v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim())
                          .Any(tok => tok == etagValue || tok == "*")))
        {
            WriteValidators(resp, etagValue, lastModified);
            resp.StatusCode = StatusCodes.Status304NotModified;
            return true;
        }

        // ---------- Conditional check: Last-Modified fallback ----------
        if (isSafe && lastModified.HasValue &&
            req.Headers.TryGetValue(HeaderNames.IfModifiedSince, out var imsRaw) &&
            DateTimeOffset.TryParse(imsRaw, out var ims))
        {
            var imsTrunc = TruncateToSeconds(ims.ToUniversalTime());
            var lmTrunc = TruncateToSeconds(lastModified.Value.ToUniversalTime());
            if (lmTrunc <= imsTrunc)
            {
                WriteValidators(resp, normalizedETag, lastModified);
                resp.StatusCode = StatusCodes.Status304NotModified;
                return true;
            }
        }

        // Miss → set validators for the fresh response the caller will write
        WriteValidators(resp, normalizedETag, lastModified);
        return false;
    }

    private static string ComputeETagFromBytes(ReadOnlySpan<byte> data, bool weakETag)
    {
        var hash = SHA256.HashData(data.ToArray());
        var tag = $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
        return weakETag ? "W/" + tag : tag;
    }

    // ---- helpers ----
    private static string? NormalizeETag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var v = raw.Trim();
        return v.StartsWith("W/", StringComparison.Ordinal)
            ? v
            : v.StartsWith('"') && v.EndsWith('"') ? v : $"\"{v}\"";
    }
    private static void WriteValidators(HttpResponse resp, string? etag, DateTimeOffset? lastModified)
    {
        if (etag is not null)
        {
            resp.Headers[HeaderNames.ETag] = etag;
        }

        if (lastModified.HasValue)
        {
            resp.Headers[HeaderNames.LastModified] = lastModified.Value.ToString("R");
        }
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset dto)
        => dto.Subtract(TimeSpan.FromTicks(dto.Ticks % TimeSpan.TicksPerSecond));
    private static byte[] ReadAllBytesPreservePosition(Stream s)
    {
        if (s is MemoryStream ms && ms.TryGetBuffer(out var seg))
        {
            return [.. seg];
        }

        long? pos = s.CanSeek ? s.Position : null;
        using var buffer = new MemoryStream();
        s.CopyTo(buffer);
        var bytes = buffer.ToArray();
        if (pos is not null && s.CanSeek)
        {
            s.Position = pos.Value;
        }

        return bytes;
    }

    /// <summary>
    /// Chooses an encoding from the Accept-Charset header value. Defaults to UTF-8 if no match found or header missing.
    /// Supports a small set of common charsets; extend the Map function as needed.
    /// Supports q-values and wildcard. E.g., "utf-8;q=0.9, iso-8859-1;q=0.5, *;q=0.1"
    /// </summary>
    /// <param name="acceptCharset">The Accept-Charset header value.</param>
    /// <returns>The chosen encoding.</returns>
    private static Encoding ChooseEncodingFromAcceptCharset(Microsoft.Extensions.Primitives.StringValues acceptCharset)
    {
        if (acceptCharset.Count == 0)
        {
            return Encoding.UTF8; // Fast path: header missing
        }

        var candidates = ParseAcceptCharsetHeader(acceptCharset);
        var (best, _) = SelectBestEncodingCandidate(candidates, static n => MapEncodingName(n));
        return best ?? Encoding.UTF8;
    }

    /// <summary>
    /// Maps a charset token to an <see cref="Encoding"/> instance if it is recognized, otherwise null.
    /// </summary>
    private static Encoding? MapEncodingName(string name) => name.ToLowerInvariant() switch
    {
        "utf-8" or "utf8" => Encoding.UTF8,
        "utf-16" => Encoding.Unicode,
        "utf-16le" => Encoding.Unicode,
        "utf-16be" => Encoding.BigEndianUnicode,
        "iso-8859-1" => Encoding.GetEncoding("iso-8859-1"),
        "us-ascii" or "ascii" => Encoding.ASCII,
        _ => null
    };

    /// <summary>
    /// Parses an Accept-Charset header (possibly multi-valued) into a sequence of (name,q) tuples.
    /// Assumes implicit q=1.0 when missing; ignores empty tokens.
    /// </summary>
    private static IEnumerable<(string name, double q)> ParseAcceptCharsetHeader(Microsoft.Extensions.Primitives.StringValues values)
    {
        return values
            .SelectMany(static line => line?.Split(',') ?? [])
            .Select(static tok =>
            {
                var t = tok.Trim();
                if (string.IsNullOrEmpty(t))
                {
                    return (name: string.Empty, q: 0.0);
                }

                var parts = t.Split(';', 2, StringSplitOptions.TrimEntries);
                var name = parts[0].ToLowerInvariant();
                var q = 1.0;
                if (parts.Length == 2 && parts[1].StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(parts[1].AsSpan(2), out var qv))
                {
                    q = qv;
                }
                return (name, q);
            })
            .Where(static x => x.name.Length > 0);
    }

    /// <summary>
    /// Selects the highest q-valued encoding candidate from the provided sequence.
    /// Wildcard (*) yields UTF-8 at the given q if no prior candidate chosen.
    /// </summary>
    /// <param name="candidates">Sequence of (name,q) pairs.</param>
    /// <param name="resolver">Function mapping charset name to Encoding (may return null).</param>
    /// <returns>Tuple of best encoding (or null) and its q value.</returns>
    private static (Encoding? best, double q) SelectBestEncodingCandidate(
        IEnumerable<(string name, double q)> candidates,
        Func<string, Encoding?> resolver)
    {
        Encoding? best = null;
        double bestQ = -1;
        foreach (var (name, q) in candidates)
        {
            if (name == "*")
            {
                if (best is null)
                {
                    best = Encoding.UTF8; bestQ = q;
                }
                continue;
            }
            var enc = resolver(name);
            if (enc is not null && q > bestQ)
            {
                best = enc; bestQ = q;
            }
        }
        return (best, bestQ);
    }
}
