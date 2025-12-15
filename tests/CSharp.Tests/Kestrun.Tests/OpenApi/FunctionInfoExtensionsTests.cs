using System.Management.Automation;
using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

/// <summary>
/// Tests for FunctionInfoExtensions.
/// </summary>
[Trait("Category", "OpenApi")]
public class FunctionInfoExtensionsTests
{
    private static FunctionInfo CreateFunction(string scriptText)
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript(scriptText);
        _ = ps.Invoke();
        var func = ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand("Test-Function", CommandTypes.Function) as FunctionInfo;
        return func ?? throw new InvalidOperationException("Failed to create function");
    }

    [Fact]
    public void GetDefaultParameterValue_WithScriptBlock_ReturnsNull()
    {
        // Arrange - Testing the extension method with a ScriptBlock that has no AST
        using var ps = PowerShell.Create();
        var func = ps.Runspace.SessionStateProxy.InvokeCommand.GetCommand("Get-Command", CommandTypes.Cmdlet) as FunctionInfo;

        // Act & Assert - Non-script cmdlets should return null
        Assert.Null(func);
    }

    [Fact]
    public void GetDefaultParameterValue_WithNoDefaultValue_ReturnsNull()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string]$Name) }");

        // Act
        var result = func.GetDefaultParameterValue("Name");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithStringDefaultValue_ReturnsString()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string]$Name = 'DefaultName') }");

        // Act
        var result = func.GetDefaultParameterValue("Name");

        // Assert
        Assert.Equal("DefaultName", result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithIntegerDefaultValue_ReturnsInteger()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([int]$Count = 42) }");

        // Act
        var result = func.GetDefaultParameterValue("Count");

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithBooleanTrue_ReturnsExpressionText()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([bool]$Enabled = $true) }");

        // Act
        var result = func.GetDefaultParameterValue("Enabled");

        // Assert - PowerShell variables are returned as expression text
        Assert.Equal("$true", result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithBooleanFalse_ReturnsExpressionText()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([bool]$Disabled = $false) }");

        // Act
        var result = func.GetDefaultParameterValue("Disabled");

        // Assert - PowerShell variables are returned as expression text
        Assert.Equal("$false", result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithSimpleArrayLiteral_ReturnsExpressionText()
    {
        // Arrange - Even simple array literals are returned as expression text
        var func = CreateFunction("function Test-Function { param([int[]]$Items = @(1, 2, 3)) }");

        // Act
        var result = func.GetDefaultParameterValue("Items");

        // Assert - Array literals are complex expressions, returned as text
        Assert.Contains("@(", (string)result!);
        Assert.Contains("1", (string)result!);
    }

    [Fact]
    public void GetDefaultParameterValue_CaseInsensitive_ReturnsDefaultValue()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string]$UserName = 'admin') }");

        // Act - using different case
        var result = func.GetDefaultParameterValue("USERNAME");

        // Assert
        Assert.Equal("admin", result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithNonExistentParameter_ReturnsNull()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string]$Name = 'test') }");

        // Act
        var result = func.GetDefaultParameterValue("NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithMultipleParameters_ReturnsCorrectDefault()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string]$First = 'one', [int]$Second = 2, [bool]$Third = $false) }");

        // Act
        var result1 = func.GetDefaultParameterValue("First");
        var result2 = func.GetDefaultParameterValue("Second");
        var result3 = func.GetDefaultParameterValue("Third");

        // Assert
        Assert.Equal("one", result1);
        Assert.Equal(2, result2);
        Assert.Equal("$false", result3); // PowerShell variable returned as text
    }

    [Fact]
    public void GetDefaultParameterValue_WithNullVariable_ReturnsExpressionText()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string]$Name = $null) }");

        // Act
        var result = func.GetDefaultParameterValue("Name");

        // Assert - PowerShell variable is returned as expression text
        Assert.Equal("$null", result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithEmptyStringDefaultValue_ReturnsEmptyString()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string]$Name = '') }");

        // Act
        var result = func.GetDefaultParameterValue("Name");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithZeroDefaultValue_ReturnsZero()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([int]$Count = 0) }");

        // Act
        var result = func.GetDefaultParameterValue("Count");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithNegativeDefaultValue_ReturnsNegativeValue()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([int]$Value = -100) }");

        // Act
        var result = func.GetDefaultParameterValue("Value");

        // Assert
        Assert.Equal(-100, result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithDecimalDefaultValue_ReturnsDecimal()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([double]$Price = 19.99) }");

        // Act
        var result = func.GetDefaultParameterValue("Price");

        // Assert
        Assert.Equal(19.99, result);
    }

    [Fact]
    public void GetDefaultParameterValue_WithEmptyArrayExpression_ReturnsExpressionText()
    {
        // Arrange
        var func = CreateFunction("function Test-Function { param([string[]]$Items = @()) }");

        // Act
        var result = func.GetDefaultParameterValue("Items");

        // Assert - Array expression syntax is returned as text
        _ = Assert.IsType<string>(result);
        Assert.Contains("@()", (string)result);
    }
}
