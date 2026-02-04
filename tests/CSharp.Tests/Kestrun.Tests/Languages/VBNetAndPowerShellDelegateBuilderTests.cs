using Kestrun.Languages;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Http;
using Serilog;
using Xunit;
using System.Management.Automation;

namespace KestrunTests.Languages;

[Collection("SharedStateSerial")]
public class VBNetAndPowerShellDelegateBuilderTests
{
    [Fact]
    [Trait("Category", "Languages")]
    public async Task VB_Build_Executes_Text_Write()
    {
        var code = "Response.WriteTextResponse(\"vb-ok\")";
        var host = new KestrunHost("Tests", Log.Logger);
        var del = VBNetDelegateBuilder.Build(host, code, null, null, null);

        var ctx = new DefaultHttpContext();
        using var ms = new MemoryStream();
        ctx.Response.Body = ms;

        await del(ctx);

        ctx.Response.Body.Position = 0;
        using var sr = new StreamReader(ctx.Response.Body);
        var body = await sr.ReadToEndAsync();
        Assert.Equal("vb-ok", body);
        Assert.Equal("text/plain; charset=utf-8", ctx.Response.ContentType);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task PowerShell_Build_Missing_Runspace_Throws_InvalidOperation()
    {
        var code = "Write-Host 'hi'";
        var host = new KestrunHost("Tests", Log.Logger);
        var del = PowerShellDelegateBuilder.Build(host, code, null);
        var ctx = new DefaultHttpContext();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => del(ctx));
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task PowerShell_ErrorStream_DoesNotOverride_Response()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        // Arrange: build PS delegate that writes to the error stream
        var del = PowerShellDelegateBuilder.Build(host, "Write-Error 'boom'", null);
        var ctx = new DefaultHttpContext();

        using var ps = PowerShell.Create();
        // Force a runspace to satisfy GetPowerShellFromContext's checks
        ps.Runspace = System.Management.Automation.Runspaces.RunspaceFactory.CreateRunspace();
        ps.Runspace.Open();

        ctx.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;
        ctx.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = new Kestrun.Models.KestrunContext(host, ctx);

        // Act
        await del(ctx);

        // Assert: error stream does not override the response status by default
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public void VB_Build_Throws_On_Whitespace()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            return VBNetDelegateBuilder.Build(host, "   ", null, null, null);
        });
        _ = Assert.IsType<ArgumentNullException>(ex);
    }
}
