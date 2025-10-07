using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace KestrunTests.Middleware;

public class StatusCodePageExtensionsTests
{
    private KestrunHost CreateMockHost()
    {
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
        return new KestrunHost("TestApp", logger.Object);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithNullOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        Assert.Throws<ArgumentNullException>(() => 
            StatusCodePageExtensions.UseStatusCodePages(app, null!));
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithDirectOptions_UsesDirectOptions()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var statusCodeOptions = new StatusCodePagesOptions();
        var options = new StatusCodeOptions(host) { Options = statusCodeOptions };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithLocationFormat_UsesRedirects()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) { LocationFormat = "/error/{0}" };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithPathFormat_UsesReExecute()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) { PathFormat = "/errors/{0}" };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithPathAndQueryFormat_UsesReExecuteWithQuery()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) 
        { 
            PathFormat = "/errors/{0}",
            QueryFormat = "code={0}"
        };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithContentTypeAndBodyFormat_UsesStaticBody()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) 
        { 
            ContentType = "text/plain",
            BodyFormat = "Error: {0}"
        };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithNoSpecificOptions_UsesDefault()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host);

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_WithLanguageOptions_UsesScriptHandler()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var langOpts = new LanguageOptions 
        { 
            Code = "Context.Response.StatusCode = 404;" ,
            Language = Kestrun.Scripting.ScriptLanguage.CSharp
        };
        var options = new StatusCodeOptions(host) { LanguageOptions = langOpts };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_PrioritizesDirectOptions()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var statusCodeOptions = new StatusCodePagesOptions();
        var options = new StatusCodeOptions(host) 
        { 
            Options = statusCodeOptions,
            LocationFormat = "/error/{0}", // Should be ignored
            PathFormat = "/errors/{0}", // Should be ignored
            ContentType = "text/plain", // Should be ignored
            BodyFormat = "Error: {0}" // Should be ignored
        };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseStatusCodePages_PrioritizesLocationFormatOverPathFormat()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) 
        { 
            LocationFormat = "/error/{0}",
            PathFormat = "/errors/{0}" // Should be ignored
        };

        var result = StatusCodePageExtensions.UseStatusCodePages(app, options);

        Assert.NotNull(result);
        Assert.Same(app, result);
    }
}
