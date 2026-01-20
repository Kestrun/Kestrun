using Kestrun.Hosting;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace KestrunTests.Hosting;

// Kestrun uses Razor runtime compilation intentionally to support
// PowerShell-backed Razor pages (dynamic execution model).
// .NET 10 marks runtime compilation obsolete (ASPDEPR003); we suppress here by design.
// Track removal/alternative: https://aka.ms/aspnet/deprecate/003

#if NET10_0_OR_GREATER
#pragma warning disable ASPDEPR003
#endif

// These tests create/delete temp directories and construct KestrunHost with a temp root.
// KestrunHost may set the process CWD to that root; if the directory is deleted without restoring
// the original CWD, later tests can fail on Unix/macOS when getcwd() or ContentRootPath validation runs.
[Collection("WorkingDirectorySerial")]
public class KestrunHost_RazorTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void AddRazorPages_RegistersService()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.AddRazorPages();
        var built = host.Build();
        var svc = built.Services.GetService<Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure.PageLoader>();
        Assert.NotNull(svc);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddRazorPages_WithConfig_RegistersService()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        _ = host.AddRazorPages(opts => opts.RootDirectory = "/CustomPages");
        var built = host.Build();
        var svc = built.Services.GetService<Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure.PageLoader>();
        Assert.NotNull(svc);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddRazorPages_WithOptionsObject_RegistersService()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        var opts = new RazorPagesOptions { RootDirectory = "/ObjPages" };
        _ = host.AddRazorPages(opts);
        var built = host.Build();
        var svc = built.Services.GetService<Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure.PageLoader>();
        Assert.NotNull(svc);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddPowerShellRazorPages_Default_DoesNotThrow()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
#pragma warning disable IDE0200
        var ex = Record.Exception(() => host.AddPowerShellRazorPages());
#pragma warning restore IDE0200
        Assert.Null(ex);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddPowerShellRazorPages_WithPrefix_DoesNotThrow()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        var ex = Record.Exception(() => host.AddPowerShellRazorPages("/ps"));
        Assert.Null(ex);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddPowerShellRazorPages_WithOptionsObject_DoesNotThrow()
    {
        var host = new KestrunHost("TestApp", AppContext.BaseDirectory);
        var opts = new RazorPagesOptions { RootDirectory = "/PSPages" };
        var ex = Record.Exception(() => host.AddPowerShellRazorPages(rootPath: null, routePrefix: "/ps", cfg: opts));
        Assert.Null(ex);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddPowerShellRazorPages_Default_AddsPagesFileProvider_WhenPagesDirectoryExists()
    {
        // Capture CWD up front; KestrunHost(tmpRoot) may change it.
        var originalCwd = Directory.GetCurrentDirectory();
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_host_pages_");

        try
        {
            var pagesDir = Path.Combine(tmpRoot.FullName, "Pages");
            _ = Directory.CreateDirectory(pagesDir);

            using var host = new KestrunHost("TestApp", tmpRoot.FullName);
            _ = host.AddPowerShellRazorPages();
            host.EnableConfiguration();

            var app = GetBuiltWebApplication(host);

            var options = app.Services.GetRequiredService<IOptions<MvcRazorRuntimeCompilationOptions>>().Value;
            Assert.Contains(options.FileProviders, fp => IsPhysicalProviderFor(fp, pagesDir));

            using var providerToDispose = TryGetPhysicalProviderFor(options.FileProviders, pagesDir);
        }
        finally
        {
            // Restore CWD before deleting the temp root to avoid leaving the process in a deleted directory.
            Directory.SetCurrentDirectory(originalCwd);
            TryDeleteDirectory(tmpRoot);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddPowerShellRazorPages_CustomRoot_AddsPagesFileProvider_WhenDirectoryExists()
    {
        // Capture CWD up front; KestrunHost(tmpRoot) may change it.
        var originalCwd = Directory.GetCurrentDirectory();
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_host_pages_custom_");

        try
        {
            var customPagesDir = Path.Combine(tmpRoot.FullName, "MyPages");
            _ = Directory.CreateDirectory(customPagesDir);

            using var host = new KestrunHost("TestApp", tmpRoot.FullName);
            _ = host.AddPowerShellRazorPages(rootPath: customPagesDir, routePrefix: null);
            host.EnableConfiguration();

            var app = GetBuiltWebApplication(host);

            var options = app.Services.GetRequiredService<IOptions<MvcRazorRuntimeCompilationOptions>>().Value;
            Assert.Contains(options.FileProviders, fp => IsPhysicalProviderFor(fp, customPagesDir));

            using var providerToDispose = TryGetPhysicalProviderFor(options.FileProviders, customPagesDir);
        }
        finally
        {
            // Restore CWD before deleting the temp root to avoid leaving the process in a deleted directory.
            Directory.SetCurrentDirectory(originalCwd);
            TryDeleteDirectory(tmpRoot);
        }
    }

    private static WebApplication GetBuiltWebApplication(KestrunHost host)
    {
        var field = typeof(KestrunHost).GetField("_app", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        var app = field.GetValue(host) as WebApplication;
        Assert.NotNull(app);

        return app;
    }

    private static PhysicalFileProvider? TryGetPhysicalProviderFor(IEnumerable<IFileProvider> providers, string expectedRoot)
    {
        foreach (var provider in providers)
        {
            if (provider is PhysicalFileProvider pfp && IsPhysicalProviderFor(pfp, expectedRoot))
            {
                return pfp;
            }
        }

        return null;
    }

    private static void TryDeleteDirectory(DirectoryInfo directory)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                directory.Delete(recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static bool IsPhysicalProviderFor(PhysicalFileProvider provider, string expectedRoot)
    {
        var actual = Path.GetFullPath(provider.Root);
        var expected = Path.GetFullPath(expectedRoot);
        return StringComparer.OrdinalIgnoreCase.Equals(actual.TrimEnd(Path.DirectorySeparatorChar), expected.TrimEnd(Path.DirectorySeparatorChar));
    }

    private static bool IsPhysicalProviderFor(IFileProvider provider, string expectedRoot) =>
        provider is PhysicalFileProvider pfp && IsPhysicalProviderFor(pfp, expectedRoot);
}

#if NET10_0_OR_GREATER
#pragma warning restore ASPDEPR003
#endif
