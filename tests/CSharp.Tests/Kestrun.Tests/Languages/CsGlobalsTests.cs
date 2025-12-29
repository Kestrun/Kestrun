using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KestrunTests.Languages;

public class CsGlobalsTests
{
    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_WithGlobals_SetsProperties()
    {
        var g = new Dictionary<string, object?> { ["a"] = "b" };
        var globals = new CsGlobals(g);
        Assert.Same(g, globals.Globals);
        Assert.NotNull(globals.Locals);
        Assert.Null(globals.Context);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_WithGlobalsAndContext_SetsContext()
    {
        var g = new Dictionary<string, object?>();
        var http = new DefaultHttpContext();
        var krCtx = TestRequestFactory.CreateContext();
        var req = krCtx.Request;
        var res = krCtx.Response;
        var host = new Kestrun.Hosting.KestrunHost("Tests", Serilog.Log.Logger);
        var ctx = new KestrunContext(host, req, res, http);

        var globals = new CsGlobals(g, ctx);
        Assert.Same(ctx, globals.Context);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Ctor_WithGlobalsContextAndLocals_SetsAll()
    {
        var g = new Dictionary<string, object?>();
        var l = new Dictionary<string, object?> { ["x"] = 1 };
        var http = new DefaultHttpContext();
        var krCtx = TestRequestFactory.CreateContext();
        var req = krCtx.Request;
        var res = krCtx.Response;
        var host = new Kestrun.Hosting.KestrunHost("Tests", Serilog.Log.Logger);
        var ctx = new KestrunContext(host, req, res, http);

        var globals = new CsGlobals(g, ctx, l);
        Assert.Same(g, globals.Globals);
        Assert.Same(l, globals.Locals);
        Assert.Same(ctx, globals.Context);
    }
}
