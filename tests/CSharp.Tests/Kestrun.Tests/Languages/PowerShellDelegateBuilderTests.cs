using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Serilog;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Xunit;

namespace KestrunTests.Languages;

public class PowerShellDelegateBuilderTests
{
    private static (DefaultHttpContext http, KestrunContext krContext) MakeContext()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/";

        // Build Kestrun context
        var req = TestRequestFactory.Create(method: http.Request.Method, path: http.Request.Path);
        var res = new KestrunResponse(req);
        var host = new KestrunHost("Tests", Log.Logger);
        var kr = new KestrunContext(host, req, res, http);
        http.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kr;
        return (http, kr);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task Build_ExecutesScript_AndAppliesDefaultResponse()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var log = new Mock<ILogger>(MockBehavior.Loose).Object;
        var (http, kr) = MakeContext();

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
}
