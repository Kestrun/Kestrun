using Kestrun.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHostRazorExtensionsTests
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
        var ex = Record.Exception(() => host.AddPowerShellRazorPages(routePrefix: "/ps", cfg: opts));
        Assert.Null(ex);
    }
}
