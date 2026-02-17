using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Serilog;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using Xunit;

namespace KestrunTests.Languages;

public class PowerShellDelegateBuilderTests
{
    private static (DefaultHttpContext http, KestrunContext krContext) MakeContext(KestrunHost host)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/";
        http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0, EndpointMetadataCollection.Empty, "TestEndpoint"));

        var kr = new KestrunContext(host, http);
        http.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kr;
        return (http, kr);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task Build_ExecutesScript_AndAppliesDefaultResponse()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var (http, kr) = MakeContext(host);

        // Prepare PowerShell with an open runspace
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        http.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;

        // trivial script
        var code = "$x = 1; $x | Out-Null;";
        var del = PowerShellDelegateBuilder.Build(host, code, arguments: null);

        await del(http);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Null(http.Response.ContentType);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task Build_WhenCustomPowerShellErrorResponseConfigured_UsesCustomScript()
    {
        var host = new KestrunHost("Tests", Log.Logger)
        {
            PowerShellErrorResponseScript = "$Context.Response.WriteJsonResponse(@{ custom = $true; message = $ErrorMessage }, $StatusCode)"
        };

        var (http, kr) = MakeContext(host);

        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        http.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;

        using var ms = new MemoryStream();
        http.Response.Body = ms;

        var del = PowerShellDelegateBuilder.Build(host, "throw 'boom'", arguments: null);

        await del(http);

        Assert.Equal(StatusCodes.Status500InternalServerError, http.Response.StatusCode);
        Assert.Contains("application/json", http.Response.ContentType, StringComparison.OrdinalIgnoreCase);

        ms.Position = 0;
        var body = await new StreamReader(ms, Encoding.UTF8).ReadToEndAsync();
        Assert.Contains("\"custom\": true", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal server error", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task Build_WhenCustomPowerShellErrorResponseFails_FallsBackToDefaultErrorWriter()
    {
        var host = new KestrunHost("Tests", Log.Logger)
        {
            PowerShellErrorResponseScript = "throw 'handler failed'"
        };

        var (http, kr) = MakeContext(host);

        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        http.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;

        using var ms = new MemoryStream();
        http.Response.Body = ms;

        var del = PowerShellDelegateBuilder.Build(host, "throw 'boom'", arguments: null);

        await del(http);

        Assert.Equal(StatusCodes.Status500InternalServerError, http.Response.StatusCode);
        Assert.NotNull(http.Response.ContentType);

        ms.Position = 0;
        var body = await new StreamReader(ms, Encoding.UTF8).ReadToEndAsync();
        Assert.Contains("internal server error", body, StringComparison.OrdinalIgnoreCase);
    }
}
