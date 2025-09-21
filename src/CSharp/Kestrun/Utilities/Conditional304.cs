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
       object? payload,
       string? etag = null,
       bool weakETag = false,
       DateTimeOffset? lastModified = null)
    {
        var req = ctx.Request;
        var resp = ctx.Response;
        var isSafe = IsSafeMethod(req.Method);

        // 1. Normalize or derive ETag
        var normalizedETag = GetOrDeriveETag(etag, payload, req, weakETag);

        // 2. If-None-Match precedence
        if (isSafe && ETagMatchesClient(req, normalizedETag))
        {
            WriteValidators(resp, normalizedETag, lastModified);
            resp.StatusCode = StatusCodes.Status304NotModified;
            return true;
        }

        // 3. If-Modified-Since fallback
        if (isSafe && LastModifiedSatisfied(req, lastModified))
        {
            WriteValidators(resp, normalizedETag, lastModified);
            resp.StatusCode = StatusCodes.Status304NotModified;
            return true;
        }

        // 4. Miss - set validators for fresh response
        WriteValidators(resp, normalizedETag, lastModified);
        return false;
    }

    /// <summary>Determines if the HTTP method is cache validator safe (GET/HEAD).</summary>
    private static bool IsSafeMethod(string method) => HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

    /// <summary>Returns provided ETag (normalized) or derives one from payload if absent.</summary>
    private static string? GetOrDeriveETag(string? etag, object? payload, HttpRequest req, bool weak)
    {
        var normalized = NormalizeETag(etag);
        if (normalized is null && payload is not null)
        {
            var bytes = ExtractBytesFromPayload(payload, req);
            normalized = ComputeETagFromBytes(bytes, weakETag: false); // derive strong first
        }
        if (weak && normalized is not null && !normalized.StartsWith("W/", StringComparison.Ordinal))
        {
            normalized = "W/" + normalized;
        }
        return normalized;
    }

    /// <summary>Extracts a raw byte array from supported payload types or throws.</summary>
    private static byte[] ExtractBytesFromPayload(object payload, HttpRequest req)
    {
        return payload switch
        {
            byte[] b => b,
            ReadOnlyMemory<byte> rom => rom.ToArray(),
            Memory<byte> mem => mem.ToArray(),
            ArraySegment<byte> seg => seg.Array is null ? [] : seg.Array.AsSpan(seg.Offset, seg.Count).ToArray(),
            string text => ChooseEncodingFromAcceptCharset(req.Headers[HeaderNames.AcceptCharset]).GetBytes(text),
            Stream s => ReadAllBytesPreservePosition(s),
            IFormFile formFile => ReadAllBytesFromFormFile(formFile),
            _ => throw new ArgumentException(
                $"Cannot derive bytes from payload of type '{payload.GetType().FullName}'. Provide an explicit ETag or pass a byte-like payload (byte[], ReadOnlyMemory<byte>, Memory<byte>, ArraySegment<byte>, Stream, IFormFile, or string).")
        };
    }

    /// <summary>
    /// Reads all bytes from an IFormFile, disposing the stream after reading.
    /// </summary>
    private static byte[] ReadAllBytesFromFormFile(IFormFile formFile)
    {
        using var stream = formFile.OpenReadStream();
        return ReadAllBytesPreservePosition(stream);
    }
    /// <summary>Determines whether client's If-None-Match header matches the normalized ETag (or *).</summary>
    private static bool ETagMatchesClient(HttpRequest req, string? normalizedETag)
    {
        return normalizedETag is not null && req.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm) &&
                             inm.Any(v => !string.IsNullOrEmpty(v) &&
                             v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(t => t.Trim())
                              .Any(tok => tok == normalizedETag || tok == "*"));
    }

    /// <summary>Checks If-Modified-Since header against lastModified (second precision).</summary>
    private static bool LastModifiedSatisfied(HttpRequest req, DateTimeOffset? lastModified)
    {
        if (!lastModified.HasValue)
        {
            return false;
        }
        if (!req.Headers.TryGetValue(HeaderNames.IfModifiedSince, out var imsRaw))
        {
            return false;
        }
        if (!DateTimeOffset.TryParse(imsRaw, out var ims))
        {
            return false;
        }
        var imsTrunc = TruncateToSeconds(ims.ToUniversalTime());
        var lmTrunc = TruncateToSeconds(lastModified.Value.ToUniversalTime());
        return lmTrunc <= imsTrunc;
    }

    /// <summary>
    /// Computes a strong or weak ETag from the given byte data using SHA-256 hashing.
    /// </summary>
    /// <param name="data">The byte data to hash.</param>
    /// <param name="weakETag">If true, the resulting ETag is marked as weak (prefixed with W/).</param>
    /// <returns>The computed ETag string, including quotes.</returns>
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
