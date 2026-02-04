using System.Text;
using Kestrun.Forms;
using Xunit;

namespace KestrunTests.Forms;

public sealed class LimitedReadStreamTests
{
    [Fact]
    [Trait("Category", "Forms")]
    public void Read_WhenLimitExceeded_ThrowsAndTracksTotalRead()
    {
        var bytes = Encoding.UTF8.GetBytes("0123456789");
        using var inner = new MemoryStream(bytes);
        using var limited = new LimitedReadStream(inner, maxBytes: 5);

        var buffer = new byte[10];

        _ = limited.Read(buffer, 0, 4);
        Assert.Equal(4, limited.TotalRead);

        var ex = Assert.Throws<KrFormLimitExceededException>(() => limited.Read(buffer, 0, 4));
        Assert.Contains("Decompressed part size exceeded limit", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(limited.TotalRead > 5);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public async Task ReadAsync_WhenLimitExceeded_Throws()
    {
        var bytes = Encoding.UTF8.GetBytes("0123456789");
        using var inner = new MemoryStream(bytes);
        using var limited = new LimitedReadStream(inner, maxBytes: 5);

        var buffer = new byte[10];

        var first = await limited.ReadAsync(buffer.AsMemory(0, 4), CancellationToken.None);
        Assert.Equal(4, first);

        var ex = await Assert.ThrowsAsync<KrFormLimitExceededException>(async () =>
        {
            _ = await limited.ReadAsync(buffer.AsMemory(0, 4), CancellationToken.None);
        });

        Assert.Contains("Decompressed part size exceeded limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
