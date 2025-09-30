using Kestrun.Hosting;
using Xunit;

namespace KestrunTests.Hosting;

public class EndpointSpecParserTests
{
    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("https://localhost:5000", "localhost", 5000, true)]
    [InlineData("http://localhost:5000", "localhost", 5000, false)]
    [InlineData("localhost:5000", "localhost", 5000, null)]
    [InlineData("127.0.0.1:8080", "127.0.0.1", 8080, null)]
    [InlineData("[::1]:6000", "::1", 6000, null)]
    [InlineData("https://localhost", "localhost", 443, true)]
    [InlineData("http://localhost", "localhost", 80, false)]
    public void TryParse_Valid(string spec, string expectedHost, int expectedPort, bool? expectedHttps)
    {
        var ok = KestrunHostMapExtensions.EndpointSpecParser.TryParse(spec, out var host, out var port, out var https);
        Assert.True(ok);
        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
        Assert.Equal(expectedHttps, https);
    }

    [Theory]
    [Trait("Category", "Hosting")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("localhost")] // missing port
    [InlineData(":5000")] // missing host
    [InlineData("localhost:-1")]
    [InlineData("localhost:0")]
    [InlineData("localhost:99999")]
    [InlineData("ftp://localhost:5000")] // unsupported scheme
    [InlineData("https://localhost:")] // empty port
    public void TryParse_Invalid(string spec)
    {
        var ok = KestrunHostMapExtensions.EndpointSpecParser.TryParse(spec, out var host, out var port, out var https);
        Assert.False(ok);
        Assert.Equal(string.Empty, host);
        Assert.Equal(0, port);
        Assert.Null(https);
    }
}
