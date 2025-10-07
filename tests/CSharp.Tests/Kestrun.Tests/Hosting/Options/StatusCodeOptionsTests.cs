using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Moq;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class StatusCodeOptionsTests
{
    private KestrunHost CreateMockHost()
    {
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
        return new KestrunHost("TestApp", logger.Object);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_WithHost_SetsHostProperty()
    {
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host);
        
        Assert.Same(host, options.Host);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StatusCodeOptions(null!));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ContentType_CanBeSet()
    {
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) { ContentType = "text/html" };
        
        Assert.Equal("text/html", options.ContentType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void BodyFormat_CanBeSet()
    {
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) { BodyFormat = "Error: {0}" };
        
        Assert.Equal("Error: {0}", options.BodyFormat);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void LocationFormat_CanBeSet()
    {
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) { LocationFormat = "/error/{0}" };
        
        Assert.Equal("/error/{0}", options.LocationFormat);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void PathFormat_CanBeSet()
    {
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) { PathFormat = "/errors/{0}" };
        
        Assert.Equal("/errors/{0}", options.PathFormat);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void QueryFormat_CanBeSet()
    {
        var host = CreateMockHost();
        var options = new StatusCodeOptions(host) { QueryFormat = "code={0}" };
        
        Assert.Equal("code={0}", options.QueryFormat);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void LanguageOptions_CanBeSet()
    {
        var host = CreateMockHost();
        var langOpts = new LanguageOptions();
        var options = new StatusCodeOptions(host) { LanguageOptions = langOpts };
        
        Assert.Same(langOpts, options.LanguageOptions);
    }
}
