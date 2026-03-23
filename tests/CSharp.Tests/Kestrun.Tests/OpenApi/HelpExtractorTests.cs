using System.Management.Automation;
using Kestrun.OpenApi;
using Xunit;

namespace Kestrun.Tests.OpenApi;

[Trait("Category", "OpenApi")]
public class HelpExtractorTests
{
    private static FunctionInfo CreateFunction(string scriptText, string functionName = "Test-Function")
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript(scriptText);
        _ = ps.Invoke();

        return (ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand(functionName, CommandTypes.Function) as FunctionInfo)
            ?? throw new InvalidOperationException("Failed to create function");
    }

    [Fact]
    public void GetHelp_WithoutCommentBasedHelp_ReturnsNull()
    {
        var fn = CreateFunction("function Test-Function { param([string]$Name) } ");

        var help = fn.GetHelp();

        Assert.Null(help);
    }

    [Fact]
    public void GetSynopsis_And_GetDescription_WithNull_ReturnEmptyString()
    {
        Assert.Equal(string.Empty, HelpExtractor.GetSynopsis(null));
        Assert.Equal(string.Empty, HelpExtractor.GetDescription(null));
    }

    [Fact]
    public void GetParameterDescription_WithNullHelp_ReturnsNull() => Assert.Null(HelpExtractor.GetParameterDescription(null, "Name"));

    [Fact]
    public void GetParameterDescription_WithEmptyName_ReturnsNull()
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript("function Test-Function { param([string]$Name) }");
        _ = ps.Invoke();
        var fn = (ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand("Test-Function", CommandTypes.Function) as FunctionInfo)
            ?? throw new InvalidOperationException("Failed to create function");

        var help = fn.GetHelp();
        Assert.Null(help.GetParameterDescription(string.Empty));
    }
}
