using System.Text;
using Kestrun.Languages;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Http;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

[Collection("SharedStateSerial")]
public class CSharpDelegateBuilderTests
{
    [Fact]
    [Trait("Category", "Languages")]
    public void CSharp_Build_Throws_On_Empty_Code()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            return CSharpDelegateBuilder.Build(host, " ", null, null, null);
        });
        _ = Assert.IsType<ArgumentNullException>(ex);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task CSharp_Build_Executes_Text_Write()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var code = "await Context.Response.WriteTextResponseAsync(\"ok\");";
        var del = CSharpDelegateBuilder.Build(host, code, null, null, null);

        var ctx = new DefaultHttpContext();
        using var ms = new MemoryStream();
        ctx.Response.Body = ms;

        await del(ctx);

        ctx.Response.Body.Position = 0;
        using var sr = new StreamReader(ctx.Response.Body, Encoding.UTF8);
        var body = await sr.ReadToEndAsync();
        Assert.Equal("ok", body);
        Assert.Equal("text/plain; charset=utf-8", ctx.Response.ContentType);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task CSharp_Build_Executes_Redirect()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var code = "Context.Response.WriteRedirectResponse(\"/next\");";
        var del = CSharpDelegateBuilder.Build(host, code, null, null, null);

        var ctx = new DefaultHttpContext();
        await del(ctx);

        Assert.Equal(302, ctx.Response.StatusCode);
        Assert.Equal("/next", ctx.Response.Headers["Location"].ToString());
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void FSharp_Build_NotImplemented()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var ex = Assert.Throws<NotImplementedException>(() =>
        {
            return FSharpDelegateBuilder.Build(host, "printfn \"hi\"");
        });
        _ = Assert.IsType<NotImplementedException>(ex);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void JScript_Build_NotImplemented_When_Flag_False()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        JScriptDelegateBuilder.Implemented = false;
        var ex = Assert.Throws<NotImplementedException>(() =>
        {
            return JScriptDelegateBuilder.Build(host, "function handle(ctx,res){ }");
        });
        _ = Assert.IsType<NotImplementedException>(ex);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Python_Build_NotImplemented_When_Flag_False()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        PyDelegateBuilder.Implemented = false;
        var ex = Assert.Throws<NotImplementedException>(() =>
        {
            return PyDelegateBuilder.Build(host, "def handle(ctx,res): pass");
        });
        _ = Assert.IsType<NotImplementedException>(ex);
    }
}
