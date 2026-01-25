using System.Security.Cryptography;

namespace Kestrun.Forms;

/// <summary>
/// Represents the result of writing a part to storage.
/// </summary>
public sealed record KrPartWriteResult
{
    /// <summary>
    /// Gets the path to the stored part.
    /// </summary>
    public required string TempPath { get; init; }

    /// <summary>
    /// Gets the length of the stored part in bytes.
    /// </summary>
    public long Length { get; init; }

    /// <summary>
    /// Gets the SHA-256 hash of the stored part, if computed.
    /// </summary>
    public string? Sha256 { get; init; }
}

/// <summary>
/// Defines a streaming sink for multipart parts.
/// </summary>
public interface IKrPartSink
{
    /// <summary>
    /// Writes the part content to the sink.
    /// </summary>
    /// <param name="source">The source stream for the part.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The write result.</returns>
    Task<KrPartWriteResult> WriteAsync(Stream source, CancellationToken cancellationToken);
}

/// <summary>
/// Stores part contents on disk.
/// </summary>
public sealed class KrDiskPartSink(string destinationPath, bool computeSha256, string? fileName = null) : IKrPartSink
{
    private readonly string _destinationPath = destinationPath;
    private readonly bool _computeSha256 = computeSha256;
    private readonly string? _fileName = fileName;

    /// <inheritdoc />
    public async Task<KrPartWriteResult> WriteAsync(Stream source, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(_destinationPath);
        var fileName = string.IsNullOrWhiteSpace(_fileName)
            ? Path.GetRandomFileName()
            : _fileName;
        var uniqueName = string.IsNullOrWhiteSpace(_fileName)
            ? fileName
            : $"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var tempPath = Path.Combine(_destinationPath, uniqueName);

        await using var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var hasher = _computeSha256 ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            hasher?.AppendData(buffer, 0, read);
        }

        var sha = hasher is null ? null : Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();

        return new KrPartWriteResult
        {
            TempPath = tempPath,
            Length = total,
            Sha256 = sha
        };
    }
}
