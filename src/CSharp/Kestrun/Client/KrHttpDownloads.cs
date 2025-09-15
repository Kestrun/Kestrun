// src/CSharp/Kestrun.Net/KrHttp.Downloads.cs
using System.Net.Http.Headers;

namespace Kestrun.Client;

/// <summary>
/// Helper methods for common HTTP download scenarios.
/// </summary>
public static class KrHttpDownloads
{
    /// <summary>
    /// Streams an HTTP response body to a file, supporting very large payloads and optional resume.
    /// Returns the final file length in bytes.
    /// </summary>
    public static async Task<long> DownloadToFileAsync(
        HttpClient client,
        HttpRequestMessage request,
        string filePath,
        bool resume = false,
        int bufferBytes = 1 << 20, // 1 MiB
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (bufferBytes < 81920)
        {
            bufferBytes = 81920;
        }

        var mode = FileMode.Create;
        if (resume && File.Exists(filePath))
        {
            // Resume support: if file exists, set Range header and append to file.
            var existing = new FileInfo(filePath).Length;
            if (existing > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existing, null);
                mode = FileMode.Append;
            }
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                        .ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        await using var target = new FileStream(
            filePath,
            mode,
            FileAccess.Write,
            FileShare.None,
            bufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await source.CopyToAsync(target, bufferBytes, cancellationToken).ConfigureAwait(false);

        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
        return new FileInfo(filePath).Length;
    }
}
