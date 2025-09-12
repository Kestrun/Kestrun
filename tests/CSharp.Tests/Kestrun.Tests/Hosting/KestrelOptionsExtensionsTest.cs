using Microsoft.AspNetCore.Server.Kestrel.Core;
using Xunit;
using Kestrun.Hosting.Options;

namespace KestrunTests.Hosting;

public class KestrelOptionsExtensionsTest
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyFromTemplate_CopiesWritableProperties()
    {
        var src = new KestrelServerOptions
        {
            AddServerHeader = false,
            AllowSynchronousIO = true
        };
        src.Limits.MaxConcurrentConnections = 123;
        src.Limits.MaxRequestBodySize = 456;

        var dest = new KestrelServerOptions
        {
            AddServerHeader = true,
            AllowSynchronousIO = false
        };
        dest.Limits.MaxConcurrentConnections = 1;
        dest.Limits.MaxRequestBodySize = 2;

        dest.CopyFromTemplate(src);

        Assert.Equal(src.AddServerHeader, dest.AddServerHeader);
        Assert.Equal(src.AllowSynchronousIO, dest.AllowSynchronousIO);
        Assert.Equal(src.Limits.MaxConcurrentConnections, dest.Limits.MaxConcurrentConnections);
        Assert.Equal(src.Limits.MaxRequestBodySize, dest.Limits.MaxRequestBodySize);
    }

    private class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyFromTemplate_ThrowsOnNullArguments()
    {
        var src = new KestrelServerOptions();
        var dest = new KestrelServerOptions();

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        _ = Assert.Throws<ArgumentNullException>(() => KestrelOptionsExtensions.CopyFromTemplate(null, src));

        _ = Assert.Throws<ArgumentNullException>(() => KestrelOptionsExtensions.CopyFromTemplate(dest, null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CopyFromTemplate_SkipsApplicationServices()
    {
        // ApplicationServices is in the skip list
        var services = new TestServiceProvider();
        var src = new KestrelServerOptions();
        var dest = new KestrelServerOptions
        {
            ApplicationServices = services
        };
        src.ApplicationServices = new TestServiceProvider();

        dest.CopyFromTemplate(src);

        Assert.Same(services, dest.ApplicationServices);
    }
}
