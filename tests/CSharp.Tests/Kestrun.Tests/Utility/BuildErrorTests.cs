using System.Management.Automation;
using Microsoft.AspNetCore.Http;
using Xunit;
using Kestrun.Utilities;

namespace KestrunTests.Utility;

public class BuildErrorTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void Text_ReturnsFormattedErrors()
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'boom'");
        _ = ps.Invoke();
        var text = BuildError.Text(ps);
        Assert.Contains("boom", text);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public async Task ResponseAsync_WritesToContext()
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'oops'");
        _ = ps.Invoke();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await BuildError.ResponseAsync(ctx, ps);
        ctx.Response.Body.Position = 0;
        var body = new StreamReader(ctx.Response.Body).ReadToEnd();
        Assert.Contains("oops", body);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Result_ReturnsIResult()
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'uh'");
        _ = ps.Invoke();
        var result = BuildError.Result(ps);
        Assert.NotNull(result);
    }
}
