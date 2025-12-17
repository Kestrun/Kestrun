using Kestrun.Razor;
using Kestrun.Scripting;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace KestrunTests.Razor;

public class PowerShellRazorPageTests
{
    private sealed class TestHostEnv : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = Environments.Development;
    }

    private static (ApplicationBuilder app, WebApplication host) CreateAppWithRazorPages(string contentRootPath)
    {
        var appName = typeof(PowerShellRazorPageTests).Assembly.GetName().Name;
        var wab = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRootPath,
            EnvironmentName = Environments.Development,
            ApplicationName = appName
        });
        _ = wab.Services.AddLogging();
        _ = wab.Services.AddRazorPages();
        var host = wab.Build();

        return (new ApplicationBuilder(host.Services), host);
    }

    [Fact]
    [Trait("Category", "Razor")]
    public async Task UsePowerShellRazorPages_ExecutesSiblingScript_AndSetsModel()
    {
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_pages_");
        try
        {
            var pagesDir = Path.Combine(tmpRoot.FullName, "Pages");
            _ = Directory.CreateDirectory(pagesDir);

            // Create Foo.cshtml and Foo.cshtml.ps1
            var viewPath = Path.Combine(pagesDir, "Foo.cshtml");
            var psPath = viewPath + ".ps1";
            await File.WriteAllTextAsync(viewPath, "<h1>Foo</h1>");
            // Set $Model and ensure it becomes HttpContext.Items["PageModel"]
            await File.WriteAllTextAsync(psPath, "$Model = @{ Name = 'Bar' } | ConvertTo-Json | ConvertFrom-Json");

            var (app, host) = CreateAppWithRazorPages(tmpRoot.FullName);
            using var kesHost = new KestrunHost("Tests", Serilog.Log.Logger);
            var pool = new KestrunRunspacePoolManager(kesHost, 1, 1);

            _ = app.UsePowerShellRazorPages(pool);
            // End of pipeline: just read model and write it to response
            app.Run(ctx =>
            {
                var model = ctx.Items["PageModel"];
                Assert.NotNull(model);
                var name = "";
                try
                {
                    dynamic d = model;
                    name = d.Name?.ToString() ?? "";
                }
                catch
                {
                    if (model is System.Management.Automation.PSObject pso)
                    {
                        name = pso.Properties["Name"]?.Value?.ToString() ?? "";
                    }
                    else if (model is System.Collections.IDictionary dict && dict.Contains("Name"))
                    {
                        name = dict["Name"]?.ToString() ?? "";
                    }
                }

                ctx.Response.StatusCode = 200;
                return ctx.Response.WriteAsync(name);
            });

            var pipeline = app.Build();
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/Foo";
            ctx.Response.Body = new MemoryStream();
            await pipeline(ctx);

            ctx.Response.Body.Position = 0;
            using var reader = new StreamReader(ctx.Response.Body);
            var body = await reader.ReadToEndAsync();
            Assert.Equal("Bar", body);
        }
        finally
        {
            try { tmpRoot.Delete(recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Razor")]
    public async Task UsePowerShellRazorPages_CallsNext_When_RequestPathIsEmpty()
    {
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_pages_");
        try
        {
            var (app, _) = CreateAppWithRazorPages(tmpRoot.FullName);
            using var kesHost = new KestrunHost("Tests", Serilog.Log.Logger);
            var pool = new KestrunRunspacePoolManager(kesHost, 1, 1);

            _ = app.UsePowerShellRazorPages(pool);

            var reachedTerminal = false;
            app.Run(ctx =>
            {
                reachedTerminal = true;
                ctx.Response.StatusCode = 200;
                return ctx.Response.WriteAsync("OK");
            });

            var pipeline = app.Build();
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/";
            ctx.Response.Body = new MemoryStream();
            await pipeline(ctx);

            Assert.True(reachedTerminal);
            ctx.Response.Body.Position = 0;
            using var reader = new StreamReader(ctx.Response.Body);
            var body = await reader.ReadToEndAsync();
            Assert.Equal("OK", body);
        }
        finally
        {
            try { tmpRoot.Delete(recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Razor")]
    public async Task UsePowerShellRazorPages_CallsNext_When_ViewOrScriptMissing()
    {
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_pages_");
        try
        {
            var pagesDir = Path.Combine(tmpRoot.FullName, "Pages");
            _ = Directory.CreateDirectory(pagesDir);

            // Create only the view; omit the .ps1 script
            var viewPath = Path.Combine(pagesDir, "OnlyView.cshtml");
            await File.WriteAllTextAsync(viewPath, "<h1>OnlyView</h1>");

            var (app, _) = CreateAppWithRazorPages(tmpRoot.FullName);
            using var kesHost = new KestrunHost("Tests", Serilog.Log.Logger);
            var pool = new KestrunRunspacePoolManager(kesHost, 1, 1);

            _ = app.UsePowerShellRazorPages(pool);
            app.Run(ctx =>
            {
                ctx.Response.StatusCode = 200;
                return ctx.Response.WriteAsync("NEXT");
            });

            var pipeline = app.Build();
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/OnlyView";
            ctx.Response.Body = new MemoryStream();
            await pipeline(ctx);

            ctx.Response.Body.Position = 0;
            using var reader = new StreamReader(ctx.Response.Body);
            var body = await reader.ReadToEndAsync();
            Assert.Equal("NEXT", body);
        }
        finally
        {
            try { tmpRoot.Delete(recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Razor")]
    public async Task UsePowerShellRazorPages_CallsNext_When_CodeBehindExists()
    {
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_pages_");
        try
        {
            var pagesDir = Path.Combine(tmpRoot.FullName, "Pages");
            _ = Directory.CreateDirectory(pagesDir);

            var viewPath = Path.Combine(pagesDir, "HasCb.cshtml");
            var psPath = viewPath + ".ps1";
            var csPath = viewPath + ".cs";
            await File.WriteAllTextAsync(viewPath, "<h1>HasCb</h1>");
            await File.WriteAllTextAsync(psPath, "$Model = @{ Name = 'SHOULD_NOT_RUN' }");
            await File.WriteAllTextAsync(csPath, "// code-behind exists");

            var (app, _) = CreateAppWithRazorPages(tmpRoot.FullName);
            using var kesHost = new KestrunHost("Tests", Serilog.Log.Logger);
            var pool = new KestrunRunspacePoolManager(kesHost, 1, 1);

            _ = app.UsePowerShellRazorPages(pool);
            app.Run(ctx =>
            {
                var hasModel = ctx.Items.ContainsKey("PageModel");
                ctx.Response.StatusCode = 200;
                return ctx.Response.WriteAsync(hasModel ? "MODEL" : "NO_MODEL");
            });

            var pipeline = app.Build();
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/HasCb";
            ctx.Response.Body = new MemoryStream();
            await pipeline(ctx);

            ctx.Response.Body.Position = 0;
            using var reader = new StreamReader(ctx.Response.Body);
            var body = await reader.ReadToEndAsync();
            Assert.Equal("NO_MODEL", body);
        }
        finally
        {
            try { tmpRoot.Delete(recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Razor")]
    public async Task UsePowerShellRazorPages_Supports_CustomRootPath()
    {
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_pages_");
        try
        {
            var customPagesDir = Path.Combine(tmpRoot.FullName, "MyPages");
            _ = Directory.CreateDirectory(customPagesDir);

            var viewPath = Path.Combine(customPagesDir, "Custom.cshtml");
            var psPath = viewPath + ".ps1";
            await File.WriteAllTextAsync(viewPath, "<h1>Custom</h1>");
            await File.WriteAllTextAsync(psPath, "$Model = @{ Name = 'FromCustomRoot' }");

            var (app, _) = CreateAppWithRazorPages(tmpRoot.FullName);
            using var kesHost = new KestrunHost("Tests", Serilog.Log.Logger);
            var pool = new KestrunRunspacePoolManager(kesHost, 1, 1);

            _ = app.UsePowerShellRazorPages(pool, rootPath: customPagesDir);
            app.Run(ctx =>
            {
                var model = ctx.Items["PageModel"] as System.Collections.IDictionary;
                var name = model?["Name"]?.ToString() ?? "";
                ctx.Response.StatusCode = 200;
                return ctx.Response.WriteAsync(name);
            });

            var pipeline = app.Build();
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/Custom";
            ctx.Response.Body = new MemoryStream();
            await pipeline(ctx);

            ctx.Response.Body.Position = 0;
            using var reader = new StreamReader(ctx.Response.Body);
            var body = await reader.ReadToEndAsync();
            Assert.Equal("FromCustomRoot", body);
        }
        finally
        {
            try { tmpRoot.Delete(recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Razor")]
    public async Task UsePowerShellRazorPages_DoesNotCallNext_When_RequestAborted()
    {
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_pages_");
        try
        {
            var pagesDir = Path.Combine(tmpRoot.FullName, "Pages");
            _ = Directory.CreateDirectory(pagesDir);

            var viewPath = Path.Combine(pagesDir, "Abort.cshtml");
            var psPath = viewPath + ".ps1";
            await File.WriteAllTextAsync(viewPath, "<h1>Abort</h1>");
            await File.WriteAllTextAsync(psPath, "$Model = @{ Name = 'ShouldNotMatter' }");

            var (app, _) = CreateAppWithRazorPages(tmpRoot.FullName);
            using var kesHost = new KestrunHost("Tests", Serilog.Log.Logger);
            var pool = new KestrunRunspacePoolManager(kesHost, 1, 1);

            _ = app.UsePowerShellRazorPages(pool);

            var reachedTerminal = false;
            app.Run(ctx =>
            {
                reachedTerminal = true;
                ctx.Response.StatusCode = 200;
                return ctx.Response.WriteAsync("NEXT");
            });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var pipeline = app.Build();
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/Abort";
            ctx.RequestAborted = cts.Token;
            ctx.Response.Body = new MemoryStream();
            await pipeline(ctx);

            Assert.False(reachedTerminal);
            Assert.Equal(0, ctx.Response.Body.Length);
        }
        finally
        {
            try { tmpRoot.Delete(recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Razor")]
    public async Task UsePowerShellRazorPages_LogsErrorAndContinues_OnScriptFailure()
    {
        var tmpRoot = Directory.CreateTempSubdirectory("kestrun_pages_");
        try
        {
            var pagesDir = Path.Combine(tmpRoot.FullName, "Pages");
            _ = Directory.CreateDirectory(pagesDir);

            var viewPath = Path.Combine(pagesDir, "Err.cshtml");
            var psPath = viewPath + ".ps1";
            await File.WriteAllTextAsync(viewPath, "<h1>Err</h1>");
            // PowerShell script that writes to error stream
            await File.WriteAllTextAsync(psPath, "Write-Error 'boom'");

            var (app, _) = CreateAppWithRazorPages(tmpRoot.FullName);
            using var kesHost = new KestrunHost("Tests", Serilog.Log.Logger);
            var pool = new KestrunRunspacePoolManager(kesHost, 1, 1);

            _ = app.UsePowerShellRazorPages(pool);
            // Terminal: ensure request completes; middleware handles error itself with response
            var pipeline = app.Build();
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/Err";
            ctx.Response.Body = new MemoryStream();

            await pipeline(ctx);
            // Error handler writes a response (typically 500); ensure it’s not 200
            Assert.NotEqual(StatusCodes.Status200OK, ctx.Response.StatusCode);
        }
        finally
        {
            try { tmpRoot.Delete(recursive: true); } catch { }
        }
    }
}
