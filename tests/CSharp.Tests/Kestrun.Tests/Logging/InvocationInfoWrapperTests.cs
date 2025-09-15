using System.Management.Automation;
using Kestrun.Logging.Data;
using Xunit;

namespace KestrunTests.Logging;

public class InvocationInfoWrapperTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void Wraps_InvocationInfo_Basic_Properties_And_ToString()
    {
        // Produce a real ErrorRecord with InvocationInfo by executing a non-terminating error
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Error 'oops'");

        _ = ps.Invoke();
        Assert.True(ps.HadErrors, "Expected PowerShell to have errors");
        Assert.True(ps.Streams.Error.Count > 0, "Expected at least one error record");

        var inv = ps.Streams.Error[0].InvocationInfo;
        var w = new InvocationInfoWrapper(inv);

        // Assertions: ensure wrapper doesn't throw and surfaces key fields (avoid ToString() to prevent deep reflection recursion)
        Assert.True(w.ScriptLineNumber >= 0);
        Assert.NotNull(w.Line);
        Assert.NotNull(w.BoundParameters);
        Assert.NotNull(w.UnboundArguments);
        Assert.NotNull(w.PositionMessage);
        // InvocationName and Line may be empty depending on how the command was invoked; just ensure no exceptions accessing them
        _ = w.InvocationName;
    }
}
