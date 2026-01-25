using System.Security.Cryptography;

namespace Kestrun.Forms;

/// <summary>
/// Disk-based implementation of <see cref="IKrPartSink"/> that streams data to a temporary file.
/// </summary>
public sealed class DiskSink : IKrPartSink, IAsyncDisposable
{
    private readonly string _tempPath;
    private readonly FileStream _fileStream;
    private readonly IncrementalHash? _hash;
    private long _bytesWritten;
    private bool _completed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskSink"/> class.
    /// </summary>
    /// <param name="destinationPath">The directory path where the temporary file will be created.</param>
    /// <param name="computeSha256">Whether to compute SHA-256 hash during writing.</param>
    public DiskSink(string destinationPath, bool computeSha256)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destination path cannot be null or whitespace.", nameof(destinationPath));
        }

        // Ensure directory exists
        Directory.CreateDirectory(destinationPath);

        // Create unique temporary file
        _tempPath = Path.Combine(destinationPath, $"krup_{Guid.NewGuid():N}.tmp");
        
        // Open file stream for writing
        _fileStream = new FileStream(
            _tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920, // 80 KB buffer
            useAsync: true);

        if (computeSha256)
        {
            _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        }
    }

    /// <summary>
    /// Writes data to the temporary file asynchronously.
    /// </summary>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            throw new InvalidOperationException("Cannot write to completed sink.");
        }

        await _fileStream.WriteAsync(buffer, cancellationToken);
        _bytesWritten += buffer.Length;

        if (_hash != null)
        {
            _hash.AppendData(buffer.Span);
        }
    }

    /// <summary>
    /// Completes the write operation and returns the result.
    /// </summary>
    public async ValueTask<(string TempPath, long Length, string? Sha256)> CompleteAsync(CancellationToken cancellationToken)
    {
        if (_completed)
        {
            throw new InvalidOperationException("Sink already completed.");
        }

        _completed = true;

        // Flush and close the file stream
        await _fileStream.FlushAsync(cancellationToken);
        await _fileStream.DisposeAsync();

        string? sha256 = null;
        if (_hash != null)
        {
            var hashBytes = _hash.GetHashAndReset();
            sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        return (_tempPath, _bytesWritten, sha256);
    }

    /// <summary>
    /// Disposes the sink and cleans up resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _hash?.Dispose();
        
        if (!_completed)
        {
            await _fileStream.DisposeAsync();
        }

        // Clean up temporary file if write was not completed
        if (!_completed && File.Exists(_tempPath))
        {
            try
            {
                File.Delete(_tempPath);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
