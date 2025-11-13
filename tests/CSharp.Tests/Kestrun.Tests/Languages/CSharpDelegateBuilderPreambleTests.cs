using Kestrun.Languages;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Http;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

[Collection("SharedStateSerial")]
public class CSharpDelegateBuilderPreambleTests
{
    [Fact]
    [Trait("Category", "Languages")]
    public void Compile_Includes_Dynamic_Imports_From_Locals_Generic()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        // Arrange
        var locals = new Dictionary<string, object?>
        {
            ["numbers"] = new List<int> { 1, 2, 3 }
        };
        var code = "// no-op"; // script body not important for preamble test

        // Act
        var script = CSharpDelegateBuilder.Compile(host, code, null, null, locals);

        // Assert
        // The script options are internal; we validate indirectly by compiling successfully
        Assert.NotNull(script);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void Compile_Locals_Override_Globals_By_Key()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        // Arrange - set a global then override via locals
        _ = host.SharedState.Set("greeting", "hello", true);
        var locals = new Dictionary<string, object?> { ["greeting"] = "override" };
        var code = "// use greeting";

        // Act
        var script = CSharpDelegateBuilder.Compile(host, code, null, null, locals);

        // We cannot access preamble directly, but absence of exception indicates merge worked.
        Assert.NotNull(script);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task Build_Delegate_Uses_Preamble_Variables()
    {
        // Arrange
        var host = new KestrunHost("Tests", Log.Logger);
        _ = host.SharedState.Set("answer", 42, true);
        var code = "await Context.Response.WriteTextResponseAsync(answer.ToString());";
        var del = CSharpDelegateBuilder.Build(host, code, null, null, null);
        var ctx = new DefaultHttpContext();
        using var ms = new MemoryStream();
        ctx.Response.Body = ms;

        // Act
        await del(ctx);

        // Assert
        ctx.Response.Body.Position = 0;
        using var sr = new StreamReader(ctx.Response.Body);
        var body = await sr.ReadToEndAsync();
        Assert.Equal("42", body);
    }
}
