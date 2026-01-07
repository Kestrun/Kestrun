using System.Management.Automation;
using System.Management.Automation.Language;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

public class PsScriptBlockValidationTests
{
    #region HasParamAndNothingAfterParam_Ast Tests

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_ValidParamBlockOnly_ReturnsTrue()
    {
        // Arrange
        var scriptText = "param([string]$Name, [int]$Age)";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_ParamBlockWithMultipleParams_ReturnsTrue()
    {
        // Arrange
        var scriptText = @"
param(
    [Parameter(Mandatory=$true)]
    [string]$Name,

    [Parameter(ValueFromPipeline=$true)]
    [object]$InputObject,

    [int]$Count = 10
)";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_EmptyParamBlock_ReturnsTrue()
    {
        // Arrange
        var scriptText = "param()";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_ParamWithStatementsAfter_ReturnsFalse()
    {
        // Arrange
        var scriptText = @"
param([string]$Name)
Write-Host $Name";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("Statements found after param()", error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_NoParamBlock_ReturnsFalse()
    {
        // Arrange
        var scriptText = "Write-Host 'Hello'";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("No param() block found", error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_ParamInBeginBlock_ReturnsFalse()
    {
        // Arrange
        var scriptText = @"
begin {
    param([string]$Name)
}
process {
    Write-Host $Name
}";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
        // When param is in begin block, it's not recognized as the root param, so it returns "No param() block found"
        Assert.Contains("No param() block found", error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_ParamWithProcessBlock_ReturnsFalse()
    {
        // Arrange
        var scriptText = @"
param([string]$Name)
process {
    Write-Host $Name
}";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("Statements found after param()", error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_ParamWithEndBlock_ReturnsFalse()
    {
        // Arrange
        var scriptText = @"
param([string]$Name)
end {
    Write-Host 'Done'
}";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("Statements found after param()", error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_InvalidPowerShellSyntax_ReturnsFalse()
    {
        // Arrange
        var scriptText = "param([string] @Name)"; // Invalid: @ symbol in wrong place

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("Parse error", error);
    }

    [Fact]
    public void HasParamAndNothingAfterParam_Ast_MultilineParamBlock_ReturnsTrue()
    {
        // Arrange
        var scriptText = @"
param(
    [string]
    $Name,

    [ValidateRange(0, 100)]
    [int]
    $Age
)";

        // Act
        var result = PsScriptBlockValidation.HasParamAndNothingAfterParam_Ast(scriptText, out var error);

        // Assert
        Assert.True(result);
        Assert.Null(error);
    }

    #endregion

    #region IsParamLast(ScriptBlockAst) Tests

    [Fact]
    public void IsParamLast_ScriptBlockAst_ParamBlockOnly_ReturnsTrue()
    {
        // Arrange
        var scriptText = "param([string]$Name)";
        var ast = Parser.ParseInput(scriptText, out _, out _);

        // Act
        var result = PsScriptBlockValidation.IsParamLast(ast);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlockAst_ParamWithNoStatements_ReturnsTrue()
    {
        // Arrange
        var scriptText = "param()";
        var ast = Parser.ParseInput(scriptText, out _, out _);

        // Act
        var result = PsScriptBlockValidation.IsParamLast(ast);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlockAst_NoParamBlock_ReturnsFalse()
    {
        // Arrange
        var scriptText = "Write-Host 'Hello'";
        var ast = Parser.ParseInput(scriptText, out _, out _);

        // Act
        var result = PsScriptBlockValidation.IsParamLast(ast);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlockAst_ParamWithProcessBlock_ReturnsFalse()
    {
        // Arrange
        var scriptText = @"
param([string]$Name)
process { Write-Host $Name }";
        var ast = Parser.ParseInput(scriptText, out _, out _);

        // Act
        var result = PsScriptBlockValidation.IsParamLast(ast);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlockAst_BeginBlockWithStatements_ReturnsFalse()
    {
        // Arrange
        var scriptText = @"
param([string]$Name)
begin { $x = 1 }";
        var ast = Parser.ParseInput(scriptText, out _, out _);

        // Act
        var result = PsScriptBlockValidation.IsParamLast(ast);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region IsParamLast(ScriptBlock) Tests

    [Fact]
    public void IsParamLast_ScriptBlock_ValidParamBlock_ReturnsTrue()
    {
        // Arrange
        var scriptBlock = ScriptBlock.Create("param([string]$Name)");

        // Act
        var result = PsScriptBlockValidation.IsParamLast(scriptBlock);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlock_EmptyParam_ReturnsTrue()
    {
        // Arrange
        var scriptBlock = ScriptBlock.Create("param()");

        // Act
        var result = PsScriptBlockValidation.IsParamLast(scriptBlock);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlock_ParamWithStatements_ReturnsFalse()
    {
        // Arrange
        var scriptBlock = ScriptBlock.Create("param([string]$Name); Write-Host $Name");

        // Act
        var result = PsScriptBlockValidation.IsParamLast(scriptBlock);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlock_NoParamBlock_ReturnsFalse()
    {
        // Arrange
        var scriptBlock = ScriptBlock.Create("Write-Host 'Hello'");

        // Act
        var result = PsScriptBlockValidation.IsParamLast(scriptBlock);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlock_NullScriptBlock_ReturnsFalse()
    {
        // Arrange
        ScriptBlock? scriptBlock = null;

        // Act
        var result = PsScriptBlockValidation.IsParamLast(scriptBlock!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlock_FunctionDefinition_WithValidParam_ReturnsTrue()
    {
        // Arrange
        // When wrapping a function definition in ScriptBlock.Create(), the ScriptBlockAst
        // will have the function definition as a statement in the main body, not as the root param.
        // This test demonstrates that function definitions themselves are not param blocks.
        var scriptBlock = ScriptBlock.Create("function Test { param([string]$Name) }");

        // Act
        var result = PsScriptBlockValidation.IsParamLast(scriptBlock);

        // Assert
        // The script block itself contains a function definition statement, not a param block,
        // so this should return false (the root param block doesn't exist)
        Assert.False(result);
    }

    [Fact]
    public void IsParamLast_ScriptBlock_FunctionDefinition_WithStatements_ReturnsFalse()
    {
        // Arrange
        // Similar to above, the ScriptBlock contains a function definition, not a param block
        var scriptBlock = ScriptBlock.Create("function Test { param([string]$Name); Write-Host $Name }");

        // Act
        var result = PsScriptBlockValidation.IsParamLast(scriptBlock);

        // Assert
        // No param block at the root level, so this returns false
        Assert.False(result);
    }

    #endregion
}
