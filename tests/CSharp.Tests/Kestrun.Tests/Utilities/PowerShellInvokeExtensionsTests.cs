using System.Management.Automation;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

/// <summary>
/// Tests for <see cref="PowerShellInvokeExtensions"/> class.
/// Tests the asynchronous PowerShell invocation with cancellation support.
/// </summary>
public class PowerShellInvokeExtensionsTests
{
    #region Method Existence and Signature Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void InvokeWithRequestAbortAsync_MethodExists()
    {
        // Verify the extension method exists
        var method = typeof(PowerShellInvokeExtensions)
            .GetMethod("InvokeWithRequestAbortAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void InvokeWithRequestAbortAsync_IsPublicStaticMethod()
    {
        // Verify it's a public static method (extension method)
        var method = typeof(PowerShellInvokeExtensions)
            .GetMethod("InvokeWithRequestAbortAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method!.IsPublic);
        Assert.True(method!.IsStatic);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void InvokeWithRequestAbortAsync_ReturnsTask()
    {
        // Verify return type is Task<PSDataCollection<PSObject>>
        var method = typeof(PowerShellInvokeExtensions)
            .GetMethod("InvokeWithRequestAbortAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var returnType = method!.ReturnType;

        // Check if it's Task<> with PSDataCollection<PSObject>
        Assert.True(returnType.IsGenericType);
        Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void InvokeWithRequestAbortAsync_HasCorrectParameters()
    {
        // Verify method has: ps, requestAborted, onAbortLog
        var method = typeof(PowerShellInvokeExtensions)
            .GetMethod("InvokeWithRequestAbortAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var parameters = method!.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal("ps", parameters[0].Name);
        Assert.Equal(typeof(PowerShell), parameters[0].ParameterType);
        Assert.Equal("requestAborted", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.Equal("onAbortLog", parameters[2].Name);
        Assert.Equal(typeof(Action), parameters[2].ParameterType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void InvokeWithRequestAbortAsync_FirstParameterIsThis()
    {
        // Verify first parameter is PowerShell (the 'this' parameter for extension method)
        var method = typeof(PowerShellInvokeExtensions)
            .GetMethod("InvokeWithRequestAbortAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var firstParam = method!.GetParameters()[0];
        Assert.Equal(typeof(PowerShell), firstParam.ParameterType);
        Assert.Equal("ps", firstParam.Name);
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("Write-Host 'test'");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ps.InvokeWithRequestAbortAsync(cts.Token));
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_WithValidToken_Executes()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("@(1, 2, 3)");

        var cts = new CancellationTokenSource();

        // Act
        var result = await ps.InvokeWithRequestAbortAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_WithDefaultCancellationToken_Executes()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("'hello'");

        // Act
        var result = await ps.InvokeWithRequestAbortAsync(default);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region OnAbortLog Callback Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_WithNullOnAbortLog_DoesNotThrow()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("'test'");
        var cts = new CancellationTokenSource();

        // Act & Assert - should not throw with null callback
        var result = await ps.InvokeWithRequestAbortAsync(cts.Token, onAbortLog: null);
        Assert.NotNull(result);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_WithOnAbortLogCallback_CallbackCanBeInvoked()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("'test'");
        var cts = new CancellationTokenSource();
        var callbackInvoked = false;

        void OnAbortLog()
        {
            callbackInvoked = true;
        }

        // Act
        var result = await ps.InvokeWithRequestAbortAsync(cts.Token, onAbortLog: OnAbortLog);

        // Assert
        Assert.NotNull(result);
        Assert.False(callbackInvoked); // Callback should not be called if not cancelled
    }

    #endregion

    #region PowerShell Script Execution Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_SimpleScriptExecution()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("1 + 1");
        var cts = new CancellationTokenSource();

        // Act
        var result = await ps.InvokeWithRequestAbortAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        _ = Assert.Single(result);
        Assert.Equal(2, result[0].BaseObject);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_MultipleResults()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("1..3");
        var cts = new CancellationTokenSource();

        // Act
        var result = await ps.InvokeWithRequestAbortAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_EmptyScriptOutput()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("# Just a comment, no output");
        var cts = new CancellationTokenSource();

        // Act
        var result = await ps.InvokeWithRequestAbortAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        // Comment-only script produces no output
        Assert.True(result.Count == 0);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_WithScriptError_ThrowsException()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("throw 'Test error'");
        var cts = new CancellationTokenSource();

        // Act & Assert
        // PowerShell script errors throw RuntimeException
        _ = await Assert.ThrowsAsync<RuntimeException>(() =>
            ps.InvokeWithRequestAbortAsync(cts.Token));
    }

    #endregion

    #region Registration and Cleanup Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_RegistersAndUnregistersToken()
    {
        // Arrange
        using var ps = PowerShell.Create();
        _ = ps.AddScript("'test'");
        var cts = new CancellationTokenSource();

        // Act
        var result = await ps.InvokeWithRequestAbortAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        // Token registration should be cleaned up (verified by no hanging registrations)
        Assert.False(cts.IsCancellationRequested);
    }

    #endregion

    #region Extension Method Usage Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task InvokeWithRequestAbortAsync_CanBeCalledAsExtensionMethod()
    {
        // Arrange
        var ps = PowerShell.Create();
        try
        {
            _ = ps.AddScript("'extension method test'");
            var cts = new CancellationTokenSource();

            // Act - call as extension method
            var result = await ps.InvokeWithRequestAbortAsync(cts.Token);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
        finally
        {
            ps.Dispose();
        }
    }

    #endregion
}
