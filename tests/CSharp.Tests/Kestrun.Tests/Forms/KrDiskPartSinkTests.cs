using System.Security.Cryptography;
using System.Text;
using Kestrun.Forms;
using Xunit;

namespace KestrunTests.Forms;

public class KrDiskPartSinkTests
{
    [Fact]
    [Trait("Category", "Forms")]
    public async Task WriteAsync_WithSha256_ComputesHash_AndWritesFile()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            const string payload = "hello world";
            await using var source = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var sink = new KrDiskPartSink(temp.FullName, computeSha256: true, fileName: "upload.txt");
            var result = await sink.WriteAsync(source, CancellationToken.None);

            Assert.True(File.Exists(result.TempPath));
            Assert.Equal(payload.Length, result.Length);

            var onDisk = await File.ReadAllTextAsync(result.TempPath, Encoding.UTF8);
            Assert.Equal(payload, onDisk);

            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            Assert.Equal(expectedHash, result.Sha256);

            Assert.EndsWith(".txt", result.TempPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("upload-", Path.GetFileName(result.TempPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Forms")]
    public async Task WriteAsync_WithoutSha256_DoesNotComputeHash()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            await using var source = new MemoryStream(payload);

            var sink = new KrDiskPartSink(temp.FullName, computeSha256: false);
            var result = await sink.WriteAsync(source, CancellationToken.None);

            Assert.True(File.Exists(result.TempPath));
            Assert.Equal(payload.Length, result.Length);
            Assert.Null(result.Sha256);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}
