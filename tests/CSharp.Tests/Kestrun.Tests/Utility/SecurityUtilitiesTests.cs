using Kestrun;
using Xunit;

namespace KestrunTests.Utility;

public class SecurityUtilitiesTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void FixedTimeEquals_ByteArrays_Equal_ReturnsTrue()
    {
        byte[] a = [1, 2, 3];
        byte[] b = [1, 2, 3];
        Assert.True(FixedTimeEquals.Test(a, b));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void FixedTimeEquals_ByteArrays_Different_ReturnsFalse()
    {
        byte[] a = [1, 2, 3];
        byte[] b = [1, 2, 4];
        Assert.False(FixedTimeEquals.Test(a, b));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void FixedTimeEquals_Strings_Equal_ReturnsTrue() => Assert.True(FixedTimeEquals.Test("abc", "abc"));

    [Fact]
    [Trait("Category", "Utility")]
    public void FixedTimeEquals_Strings_Null_ReturnsFalse()
    {
        Assert.False(FixedTimeEquals.Test((string?)null, "abc"));
        Assert.False(FixedTimeEquals.Test("abc", (string?)null));
    }
}
