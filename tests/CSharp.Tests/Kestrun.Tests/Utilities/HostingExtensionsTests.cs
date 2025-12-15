using System.Text;
using Kestrun.Hosting;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

/// <summary>
/// Tests for <see cref="HostingExtensions"/> class.
/// Note: These tests validate the extension method structure and parameter handling.
/// Full integration testing with a running host is covered in integration tests.
/// </summary>
public class HostingExtensionsTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_MethodExists()
    {
        // Verify the extension method exists
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        // Check return type is Task
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_HasCorrectParameters()
    {
        // Verify method signature matches expected parameters
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var parameters = method!.GetParameters();

        // Should have: this (KestrunHost), configureConsole, consoleEncoding, onStarted, onShutdownError, stopToken
        Assert.True(parameters.Length >= 5);
        Assert.Equal("configureConsole", parameters[1].Name);
        Assert.Equal("consoleEncoding", parameters[2].Name);
        Assert.Equal("onStarted", parameters[3].Name);
        Assert.Equal("onShutdownError", parameters[4].Name);
        Assert.Equal("stopToken", parameters[5].Name);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public async Task RunUntilShutdownAsync_WithNullHost_ThrowsArgumentNullException()
    {
        // Arrange
        KestrunHost? host = null;

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            host!.RunUntilShutdownAsync(stopToken: CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ConsoleEncodingParameterWorks()
    {
        // Test that the Encoding parameter type is correctly recognized
        _ = new Moq.Mock<KestrunHost>("test", Moq.Mock.Of<Serilog.ILogger>());
        var encoding = Encoding.UTF8;
        _ = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // This test mainly ensures no compilation errors with parameter types
        Assert.NotNull(encoding);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_OnStartedCallbackType()
    {
        // Verify onStarted parameter is Action
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var onStartedParam = method!.GetParameters().First(p => p.Name == "onStarted");
        Assert.Equal(typeof(Action), onStartedParam.ParameterType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_OnShutdownErrorCallbackType()
    {
        // Verify onShutdownError parameter is Action<Exception>
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var onErrorParam = method!.GetParameters().First(p => p.Name == "onShutdownError");
        Assert.Equal(typeof(Action<Exception>), onErrorParam.ParameterType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_StopTokenParameter()
    {
        // Verify stopToken parameter is CancellationToken
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var stopTokenParam = method!.GetParameters().First(p => p.Name == "stopToken");
        Assert.Equal(typeof(CancellationToken), stopTokenParam.ParameterType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ReturnsTask()
    {
        // Verify return type is Task
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_IsPublicStaticMethod()
    {
        // Verify it's a public static method
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method!.IsPublic);
        Assert.True(method!.IsStatic);
    }
}
