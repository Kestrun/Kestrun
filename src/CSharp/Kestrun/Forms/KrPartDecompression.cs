using System.IO.Compression;

namespace Kestrun.Forms;

/// <summary>
/// Provides per-part decompression helpers.
/// </summary>
public static class KrPartDecompression
{
    /// <summary>
    /// Wraps a stream in a decompression stream based on the content encoding.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="contentEncoding">The content encoding header value.</param>
    /// <returns>The decoded stream and normalized encoding.</returns>
    public static (Stream Stream, string Encoding) CreateDecodedStream(Stream source, string? contentEncoding)
    {
        if (string.IsNullOrWhiteSpace(contentEncoding))
        {
            return (source, "identity");
        }

        var normalized = contentEncoding.Trim().ToLowerInvariant();
        return normalized switch
        {
            "identity" => (source, "identity"),
            "gzip" => (new GZipStream(source, CompressionMode.Decompress, leaveOpen: false), "gzip"),
            "deflate" => (new DeflateStream(source, CompressionMode.Decompress, leaveOpen: false), "deflate"),
            "br" => (new BrotliStream(source, CompressionMode.Decompress, leaveOpen: false), "br"),
            _ => (source, normalized)
        };
    }
}

/// <summary>
/// Stream wrapper that enforces a maximum number of bytes read.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LimitedReadStream"/> class.
/// </remarks>
/// <param name="inner">The inner stream.</param>
/// <param name="maxBytes">The maximum number of bytes allowed.</param>
public sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
{
    private readonly Stream _inner = inner;
    private readonly long _maxBytes = maxBytes;
    private long _totalRead;

    /// <summary>
    /// Gets the total number of bytes read from the stream.
    /// </summary>
    public long TotalRead => _totalRead;

    /// <inheritdoc />
    public override bool CanRead => _inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Track(read);
        return read;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Track(read);
        return read;
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer);
        Track(read);
        return read;
    }

    private void Track(int read)
    {
        if (read <= 0)
        {
            return;
        }

        _totalRead += read;
        if (_totalRead > _maxBytes)
        {
            throw new KrFormLimitExceededException($"Decompressed part size exceeded limit of {_maxBytes} bytes.");
        }
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
