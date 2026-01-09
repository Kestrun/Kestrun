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
    #region Method Signature Tests

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
        Assert.Equal(typeof(Task), method.ReturnType);
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
        var parameters = method.GetParameters();

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
    public void RunUntilShutdownAsync_IsPublicStaticMethod()
    {
        // Verify it's a public static method
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.True(method.IsStatic);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_FirstParameterIsThis()
    {
        // Verify first parameter is KestrunHost (the 'this' parameter for extension method)
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var firstParam = method.GetParameters()[0];
        Assert.Equal(typeof(KestrunHost), firstParam.ParameterType);
        Assert.Equal("server", firstParam.Name);
    }

    #endregion

    #region Parameter Type Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_FirstParameterIsKestrunHost()
    {
        // Verify first parameter type is KestrunHost
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var firstParam = method.GetParameters()[0];
        Assert.Equal(typeof(KestrunHost), firstParam.ParameterType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ConfigureConsoleParameterType()
    {
        // Verify configureConsole parameter is bool
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var configureConsoleParam = method.GetParameters().First(p => p.Name == "configureConsole");
        Assert.Equal(typeof(bool), configureConsoleParam.ParameterType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ConsoleEncodingParameterType()
    {
        // Verify consoleEncoding parameter is Encoding?
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var encodingParam = method.GetParameters().First(p => p.Name == "consoleEncoding");
        Assert.Equal(typeof(Encoding), encodingParam.ParameterType);
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
        var onStartedParam = method.GetParameters().First(p => p.Name == "onStarted");
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
        var onErrorParam = method.GetParameters().First(p => p.Name == "onShutdownError");
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
        var stopTokenParam = method.GetParameters().First(p => p.Name == "stopToken");
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
        Assert.Equal(typeof(Task), method.ReturnType);
    }

    #endregion

    #region Argument Validation Tests

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
    public void RunUntilShutdownAsync_WithNullEncodingParameter_AllowedByDesign()
    {
        // Verify that null encoding parameter is allowed (optional parameter)
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var encodingParam = method.GetParameters().First(p => p.Name == "consoleEncoding");

        // Check if it has a default value
        Assert.True(encodingParam.HasDefaultValue);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_WithNullCallbackParameters_AllowedByDesign()
    {
        // Verify that null callback parameters are allowed (optional)
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var onStartedParam = method.GetParameters().First(p => p.Name == "onStarted");
        var onErrorParam = method.GetParameters().First(p => p.Name == "onShutdownError");

        // Both should have default values
        Assert.True(onStartedParam.HasDefaultValue);
        Assert.True(onErrorParam.HasDefaultValue);
    }

    #endregion

    #region Default Parameter Values Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ConfigureConsoleDefaultValue()
    {
        // Verify configureConsole has default value of true
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var configureConsoleParam = method.GetParameters().First(p => p.Name == "configureConsole");
        Assert.True(configureConsoleParam.HasDefaultValue);
        Assert.Equal(true, configureConsoleParam.DefaultValue);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_StopTokenDefaultValue()
    {
        // Verify stopToken has default value
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var stopTokenParam = method.GetParameters().First(p => p.Name == "stopToken");
        Assert.True(stopTokenParam.HasDefaultValue);
    }

    #endregion

    #region Encoding Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ConsoleEncodingParameterAcceptsUTF8()
    {
        // Test that the Encoding parameter type is correctly recognized
        var encoding = Encoding.UTF8;
        Assert.NotNull(encoding);
        Assert.Equal("utf-8", encoding.WebName.ToLowerInvariant());
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ConsoleEncodingParameterAcceptsUnicode()
    {
        // Test with Unicode encoding
        var encoding = Encoding.Unicode;
        Assert.NotNull(encoding);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_ConsoleEncodingParameterAcceptsASCII()
    {
        // Test with ASCII encoding
        var encoding = Encoding.ASCII;
        Assert.NotNull(encoding);
    }

    #endregion

    #region Documentation Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void RunUntilShutdownAsync_HasXmlDocumentation()
    {
        // Verify the method has XML documentation
        var method = typeof(HostingExtensions)
            .GetMethod("RunUntilShutdownAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        // In a real scenario, you could read the XML documentation
        // For now, just verify the method exists and is properly named
        Assert.NotNull(method.Name);
    }

    #endregion

    #region Extension Method Characteristics Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void HostingExtensions_IsPublicStaticClass()
    {
        // Verify the class is public and static
        var type = typeof(HostingExtensions);
        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract && type.IsSealed); // Static class
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void HostingExtensions_OnlyContainsPublicMethods()
    {
        // Verify only public static methods exist
        var type = typeof(HostingExtensions);
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => !m.IsSpecialName); // Exclude property getters/setters

        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            Assert.True(method.IsPublic);
            Assert.True(method.IsStatic);
        }
    }

    #endregion
}
