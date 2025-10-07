using Kestrun.Middleware;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Middleware;

public class CommonAccessLogOptionsTests
{
    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultLevel_IsInformation()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.Equal(LogEventLevel.Information, options.Level);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultIncludeQueryString_IsTrue()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.True(options.IncludeQueryString);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultIncludeProtocol_IsTrue()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.True(options.IncludeProtocol);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultIncludeElapsedMilliseconds_IsFalse()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.False(options.IncludeElapsedMilliseconds);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultUseUtcTimestamp_IsFalse()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.False(options.UseUtcTimestamp);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultTimestampFormat_IsApacheCommonFormat()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.Equal("dd/MMM/yyyy:HH:mm:ss zzz", options.TimestampFormat);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultTimestampFormat_MatchesConstant()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.Equal(CommonAccessLogOptions.DefaultTimestampFormat, options.TimestampFormat);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void DefaultTimeProvider_IsSystem()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.Same(TimeProvider.System, options.TimeProvider);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void Level_CanBeSet()
    {
        var options = new CommonAccessLogOptions { Level = LogEventLevel.Debug };
        
        Assert.Equal(LogEventLevel.Debug, options.Level);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void IncludeQueryString_CanBeSet()
    {
        var options = new CommonAccessLogOptions { IncludeQueryString = false };
        
        Assert.False(options.IncludeQueryString);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void IncludeProtocol_CanBeSet()
    {
        var options = new CommonAccessLogOptions { IncludeProtocol = false };
        
        Assert.False(options.IncludeProtocol);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void IncludeElapsedMilliseconds_CanBeSet()
    {
        var options = new CommonAccessLogOptions { IncludeElapsedMilliseconds = true };
        
        Assert.True(options.IncludeElapsedMilliseconds);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void UseUtcTimestamp_CanBeSet()
    {
        var options = new CommonAccessLogOptions { UseUtcTimestamp = true };
        
        Assert.True(options.UseUtcTimestamp);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void TimestampFormat_CanBeSet()
    {
        var options = new CommonAccessLogOptions { TimestampFormat = "yyyy-MM-dd HH:mm:ss" };
        
        Assert.Equal("yyyy-MM-dd HH:mm:ss", options.TimestampFormat);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void ClientAddressHeader_CanBeSet()
    {
        var options = new CommonAccessLogOptions { ClientAddressHeader = "X-Forwarded-For" };
        
        Assert.Equal("X-Forwarded-For", options.ClientAddressHeader);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void ClientAddressHeader_DefaultsToNull()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.Null(options.ClientAddressHeader);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void Logger_CanBeSet()
    {
        var logger = new Moq.Mock<Serilog.ILogger>().Object;
        var options = new CommonAccessLogOptions { Logger = logger };
        
        Assert.Same(logger, options.Logger);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public void Logger_DefaultsToNull()
    {
        var options = new CommonAccessLogOptions();
        
        Assert.Null(options.Logger);
    }
}
