using System.Security;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class SecureStringUtilsTests
{

    [Fact]
    [Trait("Category", "Utility")]
    public void ToSecureString_RoundTrip_Works()
    {
        var text = "s3cr3t!".AsSpan();
        var ss = text.ToSecureString();
        string? captured = null;
        ss.ToSecureSpan(span => captured = new string(span));
        Assert.Equal("s3cr3t!", captured);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void ToSecureSpan_ThrowsOnEmpty()
    {
        var empty = new SecureString();
        empty.MakeReadOnly();
        _ = Assert.Throws<ArgumentException>(() => empty.ToSecureSpan(_ => { }));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void ToSecureString_ThrowsOnEmpty() => _ = Assert.Throws<ArgumentException>(() => ReadOnlySpan<char>.Empty.ToSecureString());
}
