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

            // Build a fully configured service provider using WebApplication builder
            var appName = typeof(PowerShellRazorPageTests).Assembly.GetName().Name!;
            var wab = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = tmpRoot.FullName,
                EnvironmentName = Environments.Development,
                ApplicationName = appName
            });
            _ = wab.Services.AddLogging();
            _ = wab.Services.AddRazorPages();
            var host = wab.Build();

            var app = new ApplicationBuilder(host.Services);
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
                    dynamic d = model!;
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

            // Build a fully configured service provider using WebApplication builder
            var appName = typeof(PowerShellRazorPageTests).Assembly.GetName().Name!;
            var wab = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = tmpRoot.FullName,
                EnvironmentName = Environments.Development,
                ApplicationName = appName
            });
            _ = wab.Services.AddLogging();
            _ = wab.Services.AddRazorPages();
            var host = wab.Build();

            var app = new ApplicationBuilder(host.Services);
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
