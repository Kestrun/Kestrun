using Kestrun.Middleware;
using Kestrun.Scripting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrunTests.Middleware;

public class PowerShellRunspaceMiddlewareExtensionsTests
{
    [Fact]
    [Trait("Category", "Middleware")]
    public void UsePowerShellRunspace_WithPool_RegistersMiddleware()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var pool = new KestrunRunspacePoolManager(minRunspaces: 1, maxRunspaces: 1);

        var result = app.UsePowerShellRunspace(pool);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UsePowerShellRunspace_ReturnsApplicationBuilder()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var pool = new KestrunRunspacePoolManager(minRunspaces: 1, maxRunspaces: 2);

        var result = app.UsePowerShellRunspace(pool);

        // Verify the extension returns the same app builder for chaining
        Assert.Same(app, result);
    }
}
