using System.Net;
using Xunit;
using Serilog;
using Kestrun;
using Kestrun.Utilities;
// Add the following using if MapRouteOptions is in another namespace
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Kestrun.SharedState;

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostTest
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_SetsApplicationName_WhenProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        Assert.Equal("TestApp", host.ApplicationName);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_UsesDefaultApplicationName_WhenNotProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost(null, logger);

        Assert.Equal("KestrunApp", host.ApplicationName);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_SetsKestrunRoot_WhenProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var tempDir = Path.GetTempPath();
        var host = new KestrunHost("TestApp", logger, tempDir);

        Assert.Equal(tempDir, host.KestrunRoot);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureListener_AddsListenerOptions()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        host.ConfigureListener(8080, IPAddress.Loopback, false);

        Assert.Contains(host.Options.Listeners, l => l.Port == 8080 && l.IPAddress.Equals(IPAddress.Loopback));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureListener_UsesDefaultIPAddress_WhenNotProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        host.ConfigureListener(8081, false);

        Assert.Contains(host.Options.Listeners, l => l.Port == 8081 && l.IPAddress.Equals(IPAddress.Any));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddNativeRoute_ThrowsIfAppNotInitialized()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        _ = Assert.Throws<InvalidOperationException>(() =>
           host.AddMapRoute("/test", HttpVerb.Get, async ctx => { await Task.CompletedTask; })
        );
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddMapRoute_ThrowsIfAppNotInitialized()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        var options = new MapRouteOptions
        {
            Pattern = "/test",
            HttpVerbs = [HttpVerb.Get],
            Code = "Write-Output 'Hello'",
            Language = ScriptLanguage.PowerShell
        };

        _ = Assert.Throws<InvalidOperationException>(() =>
            host.AddMapRoute(options)
        );
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHtmlTemplateRoute_ThrowsIfFileNotFound()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        var options = new MapRouteOptions
        {
            Pattern = "/html",
            HttpVerbs = [HttpVerb.Get]
        };

        _ = Assert.Throws<FileNotFoundException>(() =>
            host.AddHtmlTemplateRoute(options, "nonexistent.html")
        );
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void IsCSharpScriptValid_ReturnsTrueForValid()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        var valid = host.IsCSharpScriptValid("System.Console.WriteLine(\"hi\");");
        Assert.True(valid);
    }
    [Fact]
    [Trait("Category", "Hosting")]
    public void IsCSharpScriptValid_ReturnsFalseForInvalid()
    {
        KestrunHostManager.KestrunRoot = AppContext.BaseDirectory;
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        var valid = host.IsCSharpScriptValid("System.Console.Writeline(\"hi\");");
        Assert.False(valid);
    }
    [Fact]
    [Trait("Category", "Hosting")]
    public void GetCSharpScriptErrors_ReturnsMessage()
    {
        KestrunHostManager.KestrunRoot = AppContext.BaseDirectory;
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        var msg = host.GetCSharpScriptErrors("System.Console.Writeline(\"hi\");");
        Assert.NotNull(msg);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureListener_DowngradesHttp3_WhenPreviewDisabled()
    {
        AppContext.SetSwitch("System.Runtime.EnablePreviewFeatures", false);
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);

        _ = host.ConfigureListener(12345, IPAddress.Loopback, x509Certificate: null, protocols: HttpProtocols.Http1AndHttp2AndHttp3, useConnectionLogging: true);

        var listener = host.Options.Listeners.Last();
        Assert.Equal(12345, listener.Port);
        Assert.Equal(IPAddress.Loopback, listener.IPAddress);
        Assert.Equal(HttpProtocols.Http1AndHttp2, listener.Protocols);
        Assert.True(listener.UseConnectionLogging);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureListener_AllowsHttp3_WhenPreviewEnabled()
    {
        AppContext.SetSwitch("System.Runtime.EnablePreviewFeatures", true);
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);

        _ = host.ConfigureListener(12346, IPAddress.Loopback, x509Certificate: null, protocols: HttpProtocols.Http1AndHttp2AndHttp3);

        var listener = host.Options.Listeners.Last();
        Assert.Equal(HttpProtocols.Http1AndHttp2AndHttp3, listener.Protocols);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureListener_WithCertificate_SetsHttps()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);

        using var ecdsa = System.Security.Cryptography.ECDsa.Create();
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", ecdsa, System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        _ = host.ConfigureListener(44321, IPAddress.Loopback, cert, HttpProtocols.Http1AndHttp2, useConnectionLogging: false);

        var listener = host.Options.Listeners.Last();
        Assert.True(listener.UseHttps);
        Assert.NotNull(listener.X509Certificate);
        Assert.Equal(44321, listener.Port);
        Assert.Equal(IPAddress.Loopback, listener.IPAddress);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddScheduling_Throws_OnNonPositiveMax(int bad)
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => host.AddScheduling(bad));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddScheduling_ConfiguresScheduler_AndHonorsFirstMax()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.AddScheduling(2);
        _ = host.AddScheduling(10); // should be ignored since scheduler will already be set

        host.EnableConfiguration();

        Assert.NotNull(host.Scheduler);
        Assert.Equal(2, host.Options.MaxSchedulerRunspaces);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void IsRunning_TogglesAfterStop()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();
        Assert.True(host.IsRunning);

        host.Stop();
        Assert.False(host.IsRunning);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetMapRouteOptions_ReturnsOptionsForAddedRoute()
    {
        // Sanitize globals so dynamic C# prelude uses 'object' for casts
        foreach (var key in SharedStateStore.KeySnapshot())
        {
            _ = SharedStateStore.Set(key, null);
        }
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var options = new MapRouteOptions
        {
            Pattern = "/hello",
            HttpVerbs = [HttpVerb.Get],
            Code = "Context.Response.StatusCode = 204;",
            Language = ScriptLanguage.CSharp
        };

        var map = host.AddMapRoute(options);
        Assert.NotNull(map);

        var saved = host.GetMapRouteOptions("/hello", HttpVerb.Get);
        Assert.NotNull(saved);
        Assert.Equal(ScriptLanguage.CSharp, saved!.Language);
        Assert.Contains(HttpVerb.Get, saved.HttpVerbs);
        Assert.Equal("Context.Response.StatusCode = 204;", saved.Code);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHtmlTemplateRoute_OnlyAllowsGet()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        host.EnableConfiguration();

        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "<html><body>Hello</body></html>");

        try
        {
            var bad = new MapRouteOptions { Pattern = "/tmpl", HttpVerbs = [HttpVerb.Post] };
            _ = Assert.Throws<ArgumentException>(() => host.AddHtmlTemplateRoute(bad, tmp));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task StartAsync_Then_StopAsync_Completes()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        // Bind to an ephemeral port on loopback to avoid conflicts when tests run together
        host.ConfigureListener(port: 0, ipAddress: IPAddress.Loopback, useConnectionLogging: false);

        // Use a more generous timeout for startup to avoid flakiness on slower CI hosts.
        using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(startCts.Token);

        // Poll briefly for the IsRunning flag (it should normally be true immediately after StartAsync)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!host.IsRunning && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(50);
        }
        Assert.True(host.IsRunning);

        // Separate cancellation token for shutdown so that an approaching startup timeout doesn't cancel StopAsync
        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StopAsync(stopCts.Token);

        // Allow a brief moment for IsRunning to flip
        sw.Restart();
        while (host.IsRunning && sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(25);
        }
        Assert.False(host.IsRunning);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddPowerShellRuntime_BeforeEnableConfiguration_DoesNotThrow_And_Configures()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.AddPowerShellRuntime();

        // Should not throw; runspace pool is created before middleware stages are applied
        host.EnableConfiguration();
        Assert.True(host.IsRunning);
    }
}
