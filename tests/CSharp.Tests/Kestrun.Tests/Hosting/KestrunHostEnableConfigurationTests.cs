using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Serilog;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Hosting;

/// <summary>
/// Tests for the refactored EnableConfiguration method helpers in KestrunHost.
/// These tests target the internal helper methods to ensure proper decomposition and functionality.
/// </summary>
[Collection("SharedStateSerial")]
public class KestrunHostEnableConfigurationTests
{
    private static string LocateDevModule()
    {
        // Walk upwards to find src/PowerShell/Kestrun/Kestrun.psm1 from current directory
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "src", "PowerShell", "Kestrun", "Kestrun.psm1");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            var parent = Path.GetDirectoryName(current);
            if (parent == null)
            {
                break;
            }
            current = parent;
        }
        throw new FileNotFoundException("Unable to locate dev Kestrun.psm1 in repo");
    }

    private static KestrunHost CreateTestHost(string name = "TestHost")
    {
        var module = LocateDevModule();
        var root = Directory.GetCurrentDirectory();
        return new KestrunHost(name, Log.Logger, root, [module]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateConfiguration_WhenNotConfigured_ReturnsTrue()
    {
        // Arrange
        using var host = CreateTestHost();

        // Act
        var result = host.ValidateConfiguration();

        // Assert
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateConfiguration_WhenAlreadyConfigured_ReturnsFalse()
    {
        // Arrange
        using var host = CreateTestHost();
        host.EnableConfiguration(); // Configure once

        // Act
        var result = host.ValidateConfiguration();

        // Assert
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void InitializeRunspacePool_WithValidParams_CreatesRunspacePool()
    {
        // Arrange
        using var host = CreateTestHost();
        var userVariables = new Dictionary<string, object> { ["TestVar"] = "TestValue" };
        var userFunctions = new Dictionary<string, string> { ["TestFunc"] = "param() { 'test' }" };

        // Act
        host.InitializeRunspacePool(userVariables, userFunctions);

        // Assert
        Assert.NotNull(host.RunspacePool);
        Assert.True(host.RunspacePool.MaxRunspaces > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void InitializeRunspacePool_WithNullParams_CreatesRunspacePool()
    {
        // Arrange
        using var host = CreateTestHost();

        // Act
        host.InitializeRunspacePool(null, null);

        // Assert
        Assert.NotNull(host.RunspacePool);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureKestrelBase_CallsUseKestrel()
    {
        // Arrange
        using var host = CreateTestHost();

        // Act & Assert (no exception means success)
        host.ConfigureKestrelBase();
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureNamedPipes_WithNullOptions_DoesNotThrow()
    {
        // Arrange
        using var host = CreateTestHost();
        host.Options.NamedPipeOptions = null;

        // Act & Assert
        host.ConfigureNamedPipes();
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureNamedPipes_WithOptions_ConfiguresOnWindows()
    {
        // Arrange
        using var host = CreateTestHost();
        host.Options.NamedPipeOptions = new Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.NamedPipeTransportOptions
        {
            ListenerQueueCount = 10,
            MaxReadBufferSize = 1024,
            MaxWriteBufferSize = 1024,
            CurrentUserOnly = true
        };

        // Act & Assert (no exception means success)
        host.ConfigureNamedPipes();
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureHttpsAdapter_WithNullAdapter_DoesNotThrow()
    {
        // Arrange
        using var host = CreateTestHost();
        host.Options.HttpsConnectionAdapter = null;
        
        // Create a mock server options (we can't easily instantiate the real one)
        var serverOptions = new Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions();

        // Act & Assert
        host.ConfigureHttpsAdapter(serverOptions);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void BindListeners_WithEmptyLists_DoesNotThrow()
    {
        // Arrange
        using var host = CreateTestHost();
        var serverOptions = new Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions();

        // Act & Assert
        host.BindListeners(serverOptions);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void BindListeners_WithTcpListener_BindsListener()
    {
        // Arrange
        using var host = CreateTestHost();
        var serverOptions = new Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions();
        
        host.ConfigureListener(port: 0, ipAddress: null, useConnectionLogging: false); // Port 0 for dynamic assignment

        // Act & Assert (no exception means success)
        host.BindListeners(serverOptions);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void LogConfiguredEndpoints_BuildsAppAndLogsEndpoints()
    {
        // Arrange
        using var host = CreateTestHost();
        host.InitializeRunspacePool(null, null);

        // Act & Assert (no exception means success)
        host.LogConfiguredEndpoints();
        
        // Verify app was built
        Assert.NotNull(host.App);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void HandleConfigurationError_ThrowsInvalidOperationException()
    {
        // Arrange
        using var host = CreateTestHost();
        var innerException = new ArgumentException("Test error");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => host.HandleConfigurationError(innerException));
        Assert.Equal("Failed to apply configuration.", ex.Message);
        Assert.Equal(innerException, ex.InnerException);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void EnableConfiguration_CompleteFlow_ConfiguresSuccessfully()
    {
        // Arrange
        using var host = CreateTestHost();
        var userVariables = new Dictionary<string, object> { ["TestVar"] = "TestValue" };
        var userFunctions = new Dictionary<string, string> { ["TestFunc"] = "param() { 'test' }" };

        // Act
        host.EnableConfiguration(userVariables, userFunctions);

        // Assert
        Assert.True(host.IsConfigured);
        Assert.NotNull(host.RunspacePool);
        Assert.NotNull(host.App);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void EnableConfiguration_CalledTwice_SkipsSecondCall()
    {
        // Arrange
        using var host = CreateTestHost();

        // Act
        host.EnableConfiguration();
        var initialApp = host.App;
        host.EnableConfiguration(); // Second call should be skipped

        // Assert
        Assert.True(host.IsConfigured);
        Assert.Same(initialApp, host.App); // Should be the same instance
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateConfiguration_LogsCorrectly()
    {
        // Arrange
        var logEvents = new List<LogEvent>();
        var testLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new TestLogSink(logEvents))
            .CreateLogger();

        var module = LocateDevModule();
        var root = Directory.GetCurrentDirectory();
        using var host = new KestrunHost("TestHost", testLogger, root, [module]);

        // Act
        host.ValidateConfiguration();

        // Assert
        Assert.Contains(logEvents, e => e.MessageTemplate.Text.Contains("EnableConfiguration(options) called"));
    }

    /// <summary>
    /// Test sink to capture log events for verification
    /// </summary>
    private class TestLogSink : Serilog.Core.ILogEventSink
    {
        private readonly List<LogEvent> _events;

        public TestLogSink(List<LogEvent> events)
        {
            _events = events;
        }

        public void Emit(LogEvent logEvent)
        {
            _events.Add(logEvent);
        }
    }
}