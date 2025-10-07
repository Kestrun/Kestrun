using Kestrun.Hosting.Options;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class KestrunOptionsTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void DefaultConstructor_InitializesDefaults()
    {
        var options = new KestrunOptions();

        Assert.Equal(1, options.MinRunspaces);
        Assert.Equal(8, options.MaxSchedulerRunspaces);
        Assert.Null(options.MaxRunspaces);
        Assert.Null(options.ApplicationName);
        Assert.NotNull(options.Listeners);
        Assert.Empty(options.Listeners);
        Assert.NotNull(options.ServerOptions);
        Assert.NotNull(options.ServerLimits);
        Assert.NotNull(options.ListenUnixSockets);
        Assert.Empty(options.ListenUnixSockets);
        Assert.NotNull(options.NamedPipeNames);
        Assert.Empty(options.NamedPipeNames);
        Assert.NotNull(options.Health);
        Assert.Null(options.HttpsConnectionAdapter);
        Assert.Null(options.NamedPipeOptions);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplicationName_CanBeSet()
    {
        var options = new KestrunOptions { ApplicationName = "TestApp" };

        Assert.Equal("TestApp", options.ApplicationName);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void MaxRunspaces_CanBeSet()
    {
        var options = new KestrunOptions { MaxRunspaces = 10 };

        Assert.Equal(10, options.MaxRunspaces);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void MinRunspaces_CanBeSet()
    {
        var options = new KestrunOptions { MinRunspaces = 2 };

        Assert.Equal(2, options.MinRunspaces);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void MaxSchedulerRunspaces_CanBeSet()
    {
        var options = new KestrunOptions { MaxSchedulerRunspaces = 16 };

        Assert.Equal(16, options.MaxSchedulerRunspaces);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Listeners_CanBeModified()
    {
        var options = new KestrunOptions();
        var listener = new ListenerOptions { Port = 8080 };
        options.Listeners.Add(listener);

        _ = Assert.Single(options.Listeners);
        Assert.Same(listener, options.Listeners[0]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ListenUnixSockets_CanBeModified()
    {
        var options = new KestrunOptions();
        options.ListenUnixSockets.Add("/tmp/test.sock");

        _ = Assert.Single(options.ListenUnixSockets);
        Assert.Equal("/tmp/test.sock", options.ListenUnixSockets[0]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void NamedPipeNames_CanBeModified()
    {
        var options = new KestrunOptions();
        options.NamedPipeNames.Add("TestPipe");

        _ = Assert.Single(options.NamedPipeNames);
        Assert.Equal("TestPipe", options.NamedPipeNames[0]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ServerLimits_ReturnsServerOptionsLimits()
    {
        var options = new KestrunOptions();

        Assert.Same(options.ServerOptions.Limits, options.ServerLimits);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Health_CanBeSet()
    {
        var healthOptions = new Kestrun.Health.HealthEndpointOptions { Pattern = "/custom-health" };
        var options = new KestrunOptions { Health = healthOptions };

        Assert.Same(healthOptions, options.Health);
    }
}
