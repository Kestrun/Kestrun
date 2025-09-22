using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

public class HttpVerbExtensionsTests
{
    [Theory]
    [InlineData(HttpVerb.Get, "GET")]
    [InlineData(HttpVerb.Post, "POST")]
    [InlineData(HttpVerb.Delete, "DELETE")]
    [InlineData(HttpVerb.Head, "HEAD")]
    [InlineData(HttpVerb.Put, "PUT")]
    [InlineData(HttpVerb.Patch, "PATCH")]
    [InlineData(HttpVerb.Options, "OPTIONS")]
    [InlineData(HttpVerb.Trace, "TRACE")]
    public void ToMethodString_ReturnsUpperCase(HttpVerb verb, string expected) => Assert.Equal(expected, verb.ToMethodString());
}
