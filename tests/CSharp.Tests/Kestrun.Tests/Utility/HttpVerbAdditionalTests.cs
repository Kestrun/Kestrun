using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class HttpVerbAdditionalTests
{
    [Theory]
    [InlineData("GET", HttpVerb.Get)]
    [InlineData("propfind", HttpVerb.PropFind)]
    [InlineData("VERSION-CONTROL", HttpVerb.VersionControl)]
    [InlineData("MKWORKSPACE", HttpVerb.MkWorkspace)]
    public void FromMethodString_ParsesKnown(string method, HttpVerb expected)
    {
        var v = HttpVerbExtensions.FromMethodString(method);
        Assert.Equal(expected, v);
        // ToMethodString returns enum name uppercased (may remove dashes for special cases)
        Assert.Equal(v.ToString().ToUpperInvariant(), v.ToMethodString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromMethodString_Throws_OnEmpty(string method) => Assert.Throws<ArgumentException>(() => HttpVerbExtensions.FromMethodString(method));

    [Fact]
    [Trait("Category", "Utility")]
    public void TryFromMethodString_ReturnsFalse_OnUnknown() => Assert.False(HttpVerbExtensions.TryFromMethodString("NOPE", out _));
}
