using System.Net;
using Kestrun.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using Xunit;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Builder;

namespace KestrunTests.Hosting;

/// <summary>
/// Higher-level integration style tests that build the <see cref="KestrunHost"/> and exercise the static files
/// extension methods end-to-end via HTTP using the in-process TestServer.
/// </summary>
public class KestrunHostStaticFilesExtensionsIntegrationTests
{
    private static KestrunHost CreateBuiltHost(Action<KestrunHost>? configure = null)
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestStatic", logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);
        configure?.Invoke(host);
        host.EnableConfiguration();
        return host;
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task Favicon_Default_Embedded_Served()
    {
        var host = CreateBuiltHost(h => h.AddFavicon());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);

        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:" + app.Urls.Select(u => new Uri(u).Port).First() + "/")
        };
        var resp = await client.GetAsync("favicon.ico", cts.Token);
        Assert.True(resp.IsSuccessStatusCode);
        Assert.Equal("image/x-icon", resp.Content.Headers.ContentType!.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
        Assert.True(bytes.Length > 0);
        await host.StopAsync(cts.Token);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task Favicon_Custom_Ico_ServedWithContentType()
    {
        // Create a tiny dummy favicon file (not a real ICO but sufficient for byte length test)
        var tmp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmp, [0, 1, 2, 3, 4]);
        try
        {
            var host = CreateBuiltHost(h => h.AddFavicon(tmp));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await host.StartAsync(cts.Token);
            var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var app = (WebApplication)appField.GetValue(host)!;
            var client = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:" + app.Urls.Select(u => new Uri(u).Port).First() + "/")
            };
            var resp = await client.GetAsync("favicon.ico", cts.Token);
            Assert.True(resp.IsSuccessStatusCode);
            // May resolve to image/x-icon or application/octet-stream depending on FileExtensionContentTypeProvider
            Assert.NotNull(resp.Content.Headers.ContentType);
            var bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
            Assert.Equal(5, bytes.Length);
            await host.StopAsync(cts.Token);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task FileServer_ServesStaticFile()
    {
        var tmpDir = Directory.CreateTempSubdirectory();
        var filePath = Path.Combine(tmpDir.FullName, "test.txt");
        await File.WriteAllTextAsync(filePath, "hello static");
        var provider = new PhysicalFileProvider(tmpDir.FullName);

        var host = CreateBuiltHost(h => h.AddFileServer(o =>
        {
            o.FileProvider = provider;
            o.RequestPath = "/pub";
            o.EnableDefaultFiles = false;
        }));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var basePort = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{basePort}/") };
        var resp = await client.GetAsync("pub/test.txt", cts.Token);
        Assert.True(resp.IsSuccessStatusCode);
        var text = await resp.Content.ReadAsStringAsync(cts.Token);
        Assert.Equal("hello static", text);
        await host.StopAsync(cts.Token);
        tmpDir.Delete(true);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task StaticFiles_CustomContentTypeProvider_Used()
    {
        var tmpDir = Directory.CreateTempSubdirectory();
        var filePath = Path.Combine(tmpDir.FullName, "data.xyz");
        await File.WriteAllTextAsync(filePath, "xyz content");
        var provider = new PhysicalFileProvider(tmpDir.FullName);
        var ctp = new FileExtensionContentTypeProvider();
        ctp.Mappings[".xyz"] = "application/xyz";

        var host = CreateBuiltHost(h => h.AddStaticFiles(o =>
        {
            o.FileProvider = provider;
            o.RequestPath = "/s";
            o.ContentTypeProvider = ctp;
        }));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var port = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };
        var resp = await client.GetAsync("s/data.xyz", cts.Token);
        Assert.True(resp.IsSuccessStatusCode);
        Assert.Equal("application/xyz", resp.Content.Headers.ContentType!.MediaType);
        var content = await resp.Content.ReadAsStringAsync(cts.Token);
        Assert.Equal("xyz content", content);
        await host.StopAsync(cts.Token);
        tmpDir.Delete(true);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task DefaultFiles_CustomName_Served()
    {
        var tmpDir = Directory.CreateTempSubdirectory();
        var homePath = Path.Combine(tmpDir.FullName, "home.html");
        await File.WriteAllTextAsync(homePath, "<h1>Hello Home</h1>");
        var provider = new PhysicalFileProvider(tmpDir.FullName);

        var host = CreateBuiltHost(h =>
        {
            _ = h.AddDefaultFiles(o =>
            {
                o.FileProvider = provider;
                o.RequestPath = "/d";
                o.DefaultFileNames.Clear();
                o.DefaultFileNames.Add("home.html");
            });
            _ = h.AddStaticFiles(o =>
            {
                o.FileProvider = provider;
                o.RequestPath = "/d";
            });
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var port = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };
        var resp = await client.GetAsync("d/", cts.Token); // directory request
        Assert.True(resp.IsSuccessStatusCode);
        var html = await resp.Content.ReadAsStringAsync(cts.Token);
        Assert.Contains("Hello Home", html);
        await host.StopAsync(cts.Token);
        tmpDir.Delete(true);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task FileServer_DirectoryBrowsing_ListsFile()
    {
        var tmpDir = Directory.CreateTempSubdirectory();
        var filePath = Path.Combine(tmpDir.FullName, "listme.txt");
        await File.WriteAllTextAsync(filePath, "browse me");
        var provider = new PhysicalFileProvider(tmpDir.FullName);

        var host = CreateBuiltHost(h => h.AddFileServer(o =>
        {
            o.FileProvider = provider;
            o.RequestPath = "/browse";
            o.EnableDirectoryBrowsing = true;
            o.EnableDefaultFiles = false;
        }));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var port = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };
        var resp = await client.GetAsync("browse/", cts.Token);
        Assert.True(resp.IsSuccessStatusCode);
        var listing = await resp.Content.ReadAsStringAsync(cts.Token);
        Assert.Contains("listme.txt", listing, StringComparison.OrdinalIgnoreCase);
        await host.StopAsync(cts.Token);
        tmpDir.Delete(true);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task StaticFiles_UnknownExtension_DefaultContentTypeApplied()
    {
        var tmpDir = Directory.CreateTempSubdirectory();
        var filePath = Path.Combine(tmpDir.FullName, "file.unknownext");
        await File.WriteAllTextAsync(filePath, "mystery");
        var provider = new PhysicalFileProvider(tmpDir.FullName);

        var host = CreateBuiltHost(h => h.AddStaticFiles(o =>
        {
            o.FileProvider = provider;
            o.RequestPath = "/u";
            o.ServeUnknownFileTypes = true;
            o.DefaultContentType = "application/custom";
        }));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var port = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };
        var resp = await client.GetAsync("u/file.unknownext", cts.Token);
        Assert.True(resp.IsSuccessStatusCode);
        Assert.Equal("application/custom", resp.Content.Headers.ContentType!.MediaType);
        var content = await resp.Content.ReadAsStringAsync(cts.Token);
        Assert.Equal("mystery", content);
        await host.StopAsync(cts.Token);
        tmpDir.Delete(true);
    }
}
