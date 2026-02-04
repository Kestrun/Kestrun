using System.IO.Compression;
using System.Text;
using Kestrun.Forms;
using Xunit;

namespace KestrunTests.Forms;

public sealed class KrPartDecompressionTests
{
    [Theory]
    [Trait("Category", "Forms")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("identity")]
    [InlineData("IDENTITY")]
    public void CreateDecodedStream_WhenNoOrIdentityEncoding_ReturnsIdentity(string? contentEncoding)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var (stream, encoding) = KrPartDecompression.CreateDecodedStream(source, contentEncoding);

        Assert.Same(source, stream);
        Assert.Equal("identity", encoding);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public void CreateDecodedStream_UnknownEncoding_ReturnsSourceAndNormalizes()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var (stream, encoding) = KrPartDecompression.CreateDecodedStream(source, "  X-Custom  ");

        Assert.Same(source, stream);
        Assert.Equal("x-custom", encoding);
    }

    [Theory]
    [Trait("Category", "Forms")]
    [InlineData("gzip")]
    [InlineData("deflate")]
    [InlineData("br")]
    public void CreateDecodedStream_KnownEncoding_ProducesDecodedStream(string encoding)
    {
        const string message = "hello world";
        var compressed = Compress(message, encoding);

        using var source = new MemoryStream(compressed);
        var (decoded, normalized) = KrPartDecompression.CreateDecodedStream(source, encoding.ToUpperInvariant());

        Assert.Equal(encoding, normalized);

        using var reader = new StreamReader(decoded, Encoding.UTF8);
        var text = reader.ReadToEnd();
        Assert.Equal(message, text);
    }

    private static byte[] Compress(string text, string encoding)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var output = new MemoryStream();

        Stream compressor = encoding switch
        {
            "gzip" => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
            "deflate" => new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true),
            "br" => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding))
        };

        using (compressor)
        {
            compressor.Write(input, 0, input.Length);
        }

        return output.ToArray();
    }
}
