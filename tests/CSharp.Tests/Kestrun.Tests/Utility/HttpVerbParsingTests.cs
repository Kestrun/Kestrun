using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class HttpVerbParsingTests
{
    [Theory]
    [InlineData("get", HttpVerb.Get)]
    [InlineData("POST", HttpVerb.Post)]
    [InlineData("PROPFIND", HttpVerb.PropFind)]
    [InlineData("PROPPATCH", HttpVerb.PropPatch)]
    [InlineData("MKCOL", HttpVerb.MkCol)]
    [InlineData("VERSION-CONTROL", HttpVerb.VersionControl)]
    [InlineData("MKWORKSPACE", HttpVerb.MkWorkspace)]
    [InlineData("ORDERPATCH", HttpVerb.OrderPatch)]
    public void FromMethodString_ParsesKnownVerbs(string input, HttpVerb expected)
    {
        var result = HttpVerbExtensions.FromMethodString(input);
        Assert.Equal(expected, result);
        Assert.True(HttpVerbExtensions.TryFromMethodString(input, out var tryResult));
        Assert.Equal(expected, tryResult);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void FromMethodString_ThrowsOnUnknown()
    {
        _ = Assert.Throws<ArgumentException>(() => HttpVerbExtensions.FromMethodString("NOPE"));
        Assert.False(HttpVerbExtensions.TryFromMethodString("NOPE", out _));
    }
}
