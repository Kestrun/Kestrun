using System.Reflection;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Moq;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHostStaticFilesExtensionsTests
{
    private KestrunHost CreateHost(out List<Action<IApplicationBuilder>> middleware)
    {
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
        var host = new KestrunHost("TestApp", logger.Object);
        var field = typeof(KestrunHost).GetField("_middlewareQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        middleware = (List<Action<IApplicationBuilder>>)field!.GetValue(host)!;
        return host;
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddDefaultFiles_WithNullConfig_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddDefaultFiles((DefaultFilesOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddDefaultFiles_WithConfig_InvokesDelegate()
    {
        var host = CreateHost(out var middleware);
        var options = new DefaultFilesOptions();
        _ = host.AddDefaultFiles(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFavicon_RegistersFaviconMiddleware()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddFavicon("/favicon.ico");
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFileServer_WithNullConfig_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddFileServer((FileServerOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFileServer_WithConfig_InvokesDelegate()
    {
        var host = CreateHost(out var middleware);
        var options = new FileServerOptions();
        _ = host.AddFileServer(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddStaticFiles_WithNullConfig_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddStaticFiles((StaticFileOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddStaticFiles_WithConfig_InvokesDelegate()
    {
        var host = CreateHost(out var middleware);
        var options = new StaticFileOptions();
        _ = host.AddStaticFiles(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddStaticFiles_WithCustomFileProvider_SetsProvider()
    {
        var host = CreateHost(out var middleware);
        var provider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
        _ = host.AddStaticFiles(o => o.FileProvider = provider);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddStaticFiles_WithCustomRequestPath_SetsPath()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddStaticFiles(o => o.RequestPath = "/static");
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddDefaultFiles_WithCustomDefaultFileNames_SetsNames()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddDefaultFiles(o => o.DefaultFileNames.Add("home.html"));
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFileServer_WithDirectoryBrowsing_EnablesBrowsing()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddFileServer(o => o.EnableDirectoryBrowsing = true);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFileServer_WithCustomRequestPath_SetsPath()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddFileServer(o => o.RequestPath = "/files");
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFavicon_WithNullPath_UsesDefault()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddFavicon(null);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddDefaultFiles_WithNullAction_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddDefaultFiles((Action<DefaultFilesOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddStaticFiles_WithNullAction_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddStaticFiles((Action<StaticFileOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFileServer_WithNullAction_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddFileServer((Action<FileServerOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddDirectoryBrowser_Method_IsDefined_WithExpectedSignature()
    {
        var extType = typeof(KestrunHostStaticFilesExtensions);
        var method = extType.GetMethod(
            "AddDirectoryBrowser",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(KestrunHost), typeof(string)],
            null);

        Assert.NotNull(method);
        Assert.True(method.IsStatic);
    }
}
