using System.Text;
using Kestrun.Languages;
using Microsoft.AspNetCore.Http;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

[Collection("SharedStateSerial")]
public class CSharpDelegateBuilderTests
{
    [Fact]
    public void CSharp_Build_Throws_On_Empty_Code()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            return CSharpDelegateBuilder.Build(" ", Log.Logger, null, null, null);
        });
        _ = Assert.IsType<ArgumentNullException>(ex);
    }

    [Fact]
    public async Task CSharp_Build_Executes_Text_Write()
    {
        var code = "await Context.Response.WriteTextResponseAsync(\"ok\");";
        var del = CSharpDelegateBuilder.Build(code, Log.Logger, null, null, null);

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
    public async Task CSharp_Build_Executes_Redirect()
    {
        var code = "Context.Response.WriteRedirectResponse(\"/next\");";
        var del = CSharpDelegateBuilder.Build(code, Log.Logger, null, null, null);

        var ctx = new DefaultHttpContext();
        await del(ctx);

        Assert.Equal(302, ctx.Response.StatusCode);
        Assert.Equal("/next", ctx.Response.Headers["Location"].ToString());
    }

    [Fact]
    public void FSharp_Build_NotImplemented()
    {
        var ex = Assert.Throws<NotImplementedException>(() =>
        {
            return FSharpDelegateBuilder.Build("printfn \"hi\"", Log.Logger);
        });
        _ = Assert.IsType<NotImplementedException>(ex);
    }

    [Fact]
    public void JScript_Build_NotImplemented_When_Flag_False()
    {
        JScriptDelegateBuilder.Implemented = false;
        var ex = Assert.Throws<NotImplementedException>(() =>
        {
            return JScriptDelegateBuilder.Build("function handle(ctx,res){ }", Log.Logger);
        });
        _ = Assert.IsType<NotImplementedException>(ex);
    }

    [Fact]
    public void Python_Build_NotImplemented_When_Flag_False()
    {
        PyDelegateBuilder.Implemented = false;
        var ex = Assert.Throws<NotImplementedException>(() =>
        {
            return PyDelegateBuilder.Build("def handle(ctx,res): pass", Log.Logger);
        });
        _ = Assert.IsType<NotImplementedException>(ex);
    }
}
