using System.IO.Compression;

namespace Kestrun.Forms;

/// <summary>
/// A stream wrapper that limits the number of bytes that can be read.
/// Used to enforce decompressed size limits and prevent decompression bombs.
/// </summary>
public sealed class LimitedReadStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maxBytes;
    private long _bytesRead;

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitedReadStream"/> class.
    /// </summary>
    /// <param name="innerStream">The underlying stream to read from.</param>
    /// <param name="maxBytes">The maximum number of bytes allowed to be read.</param>
    public LimitedReadStream(Stream innerStream, long maxBytes)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _maxBytes = maxBytes;
    }

    /// <inheritdoc/>
    public override bool CanRead => _innerStream.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = _maxBytes - _bytesRead;
        if (remaining <= 0)
        {
            throw new InvalidOperationException($"Decompressed size limit of {_maxBytes} bytes exceeded.");
        }

        var toRead = (int)Math.Min(count, remaining);
        var read = _innerStream.Read(buffer, offset, toRead);
        _bytesRead += read;
        return read;
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var remaining = _maxBytes - _bytesRead;
        if (remaining <= 0)
        {
            throw new InvalidOperationException($"Decompressed size limit of {_maxBytes} bytes exceeded.");
        }

        var toRead = (int)Math.Min(count, remaining);
        var read = await _innerStream.ReadAsync(buffer.AsMemory(offset, toRead), cancellationToken);
        _bytesRead += read;
        return read;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var remaining = _maxBytes - _bytesRead;
        if (remaining <= 0)
        {
            throw new InvalidOperationException($"Decompressed size limit of {_maxBytes} bytes exceeded.");
        }

        var toRead = (int)Math.Min(buffer.Length, remaining);
        var read = await _innerStream.ReadAsync(buffer[..toRead], cancellationToken);
        _bytesRead += read;
        return read;
    }

    /// <inheritdoc/>
    public override void Flush() => _innerStream.Flush();

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Do not dispose inner stream - caller owns it
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Provides helper methods for applying per-part decompression.
/// </summary>
public static class KrPartDecompression
{
    /// <summary>
    /// Wraps a stream with decompression if the content encoding requires it.
    /// </summary>
    /// <param name="bodyStream">The raw body stream.</param>
    /// <param name="contentEncoding">The Content-Encoding header value.</param>
    /// <param name="maxDecompressedBytes">Maximum allowed decompressed bytes.</param>
    /// <param name="logger">Logger instance for logging decompression events.</param>
    /// <returns>A stream (possibly wrapped with decompression) and a flag indicating if decompression is active.</returns>
    public static (Stream Stream, bool IsDecompressing) WrapWithDecompression(
        Stream bodyStream,
        string? contentEncoding,
        long maxDecompressedBytes,
        Serilog.ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(contentEncoding) || contentEncoding.Equals("identity", StringComparison.OrdinalIgnoreCase))
        {
            return (bodyStream, false);
        }

        Stream decompressedStream = contentEncoding.ToLowerInvariant() switch
        {
            "gzip" => new GZipStream(bodyStream, CompressionMode.Decompress, leaveOpen: true),
            "deflate" => new DeflateStream(bodyStream, CompressionMode.Decompress, leaveOpen: true),
            "br" => new BrotliStream(bodyStream, CompressionMode.Decompress, leaveOpen: true),
            _ => throw new NotSupportedException($"Unsupported content encoding: {contentEncoding}")
        };

        logger.Debug("Part-level decompression enabled: {Encoding}, MaxDecompressedBytes: {MaxBytes}", 
            contentEncoding, maxDecompressedBytes);

        // Wrap with size limiter
        var limitedStream = new LimitedReadStream(decompressedStream, maxDecompressedBytes);
        return (limitedStream, true);
    }
}
