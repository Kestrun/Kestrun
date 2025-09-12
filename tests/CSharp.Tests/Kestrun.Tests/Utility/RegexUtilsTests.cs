using Kestrun;
using System.Reflection;
using Xunit;

namespace KestrunTests.Utility;

public class RegexUtilsTests
{
    private static bool InvokeIsGlobMatch(string input, string pattern, bool ignoreCase = true)
    {
        var asm = typeof(FixedTimeEquals).Assembly;
        var t = asm.GetType("Kestrun.Utilities.RegexUtils")!;
        var method = t.GetMethod("IsGlobMatch", BindingFlags.Public | BindingFlags.Static)!;
        return (bool)method.Invoke(null, [input, pattern, ignoreCase])!;
    }

    [Theory]
    [InlineData("foo.txt", "*.txt", true)]
    [InlineData("foo.TXT", "*.txt", true)]
    [InlineData("bar.log", "*.txt", false)]
    [InlineData("abc", "a?c", true)]
    public void IsGlobMatch_Works(string input, string pattern, bool expected) => Assert.Equal(expected, InvokeIsGlobMatch(input, pattern));

    [Fact]
    [Trait("Category", "Utility")]
    public void IsGlobMatch_CaseSensitive_Works()
    {
        Assert.True(InvokeIsGlobMatch(input: "abc", pattern: "ABC"));
        Assert.False(InvokeIsGlobMatch(input: "abc", pattern: "ABC", ignoreCase: false));
    }
}
