using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

public class DelegateBuilderTests
{
    private static (DefaultHttpContext http, ILogger log) MakeCtx()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/test";
        TestRequestFactory.EnsureRoutedHttpContext(http);
        var log = new Mock<ILogger>(MockBehavior.Loose).Object;
        return (http, log);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task PrepareExecutionAsync_IncludesArgsAndSharedState()
    {
        var (http, log) = MakeCtx();
        var args = new Dictionary<string, object?> { ["foo"] = "bar" };

        var host = new Kestrun.Hosting.KestrunHost("Tests", log);
        var (globals, response, context) = await DelegateBuilder.PrepareExecutionAsync(host, http, args);

        Assert.NotNull(globals);
        Assert.NotNull(response);
        Assert.NotNull(context);
        Assert.Equal("/test", context.Request.Path);
        Assert.Equal("bar", globals.Globals["foo"]);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task ApplyResponseAsync_UsesRedirect_WhenSet()
    {
        var (http, log) = MakeCtx();
        var krCtx = TestRequestFactory.CreateContext();
        var req = krCtx.Request;
        var kr = krCtx.Response;
        kr.WriteRedirectResponse("/elsewhere");

        await DelegateBuilder.ApplyResponseAsync(http, kr, log);

        Assert.Equal(302, http.Response.StatusCode);
        Assert.Equal("/elsewhere", http.Response.Headers["Location"].ToString());
    }
}
