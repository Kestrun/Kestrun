using System.Text;
using Kestrun;
using Xunit;

namespace KestrunTests.Utility;

public class FixedTimeEqualsTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void Bytes_vs_Bytes_Match_ReturnsTrue()
    {
        var a = Encoding.UTF8.GetBytes("my-secret-api-key");
        var b = Encoding.UTF8.GetBytes("my-secret-api-key");
        Assert.True(FixedTimeEquals.Test(a, b));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Bytes_vs_Bytes_Mismatch_ReturnsFalse()
    {
        var a = Encoding.UTF8.GetBytes("my-secret-api-key");
        var b = Encoding.UTF8.GetBytes("my-secret-api-key-");
        Assert.False(FixedTimeEquals.Test(a, b));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void String_vs_String_Match_ReturnsTrue() => Assert.True(FixedTimeEquals.Test("my-secret-api-key", "my-secret-api-key"));

    [Fact]
    [Trait("Category", "Utility")]
    public void String_vs_String_Mismatch_ReturnsFalse() => Assert.False(FixedTimeEquals.Test("my-secret-api-key", "my-secret-api-key-"));

    [Fact]
    [Trait("Category", "Utility")]
    public void Bytes_vs_String_Match_ReturnsTrue()
    {
        var bytes = Encoding.UTF8.GetBytes("my-secret-api-key");
        Assert.True(FixedTimeEquals.Test(bytes, "my-secret-api-key"));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Bytes_vs_String_Mismatch_ReturnsFalse()
    {
        var bytes = Encoding.UTF8.GetBytes("my-secret-api-key");
        Assert.False(FixedTimeEquals.Test(bytes, "my-secret-api-key-"));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void String_vs_Bytes_Match_ReturnsTrue()
    {
        var bytes = Encoding.UTF8.GetBytes("pässwörd");
        Assert.True(FixedTimeEquals.Test("pässwörd", bytes));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void String_vs_Bytes_Mismatch_ReturnsFalse()
    {
        var bytes = Encoding.UTF8.GetBytes("pässwörd");
        Assert.False(FixedTimeEquals.Test("pässwörd-", bytes));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Null_Strings_ReturnFalse()
    {
        Assert.False(FixedTimeEquals.Test((string?)null, "x"));
        Assert.False(FixedTimeEquals.Test("x", (string?)null));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Empty_Strings_Match_ReturnsTrue() => Assert.True(FixedTimeEquals.Test("", ""));
}
