using Kestrun.Hosting;
using Kestrun.Models;
using Kestrun.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace KestrunTests.Models;

[Trait("Category", "Models")]
public sealed class KestrunContextBroadcastTests
{
    private static KestrunContext NewContext(IServiceProvider? services)
    {
        var host = new KestrunHost("Tests", AppContext.BaseDirectory);
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/";

        // KestrunContext requires a RouteEndpoint to resolve route metadata
        http.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/"),
            order: 0,
            metadata: EndpointMetadataCollection.Empty,
            displayName: "TestEndpoint"));

        http.RequestServices = services!;

        return new KestrunContext(host, http);
    }

    [Fact]
    public async Task BroadcastLogAsync_NoServiceProvider_ReturnsFalse()
    {
        var ctx = NewContext(services: null);
        var ok = await ctx.BroadcastLogAsync("Information", "hello");
        Assert.False(ok);
    }

    [Fact]
    public async Task BroadcastLogAsync_NoBroadcasterRegistered_ReturnsFalse()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var ctx = NewContext(sp);

        var ok = await ctx.BroadcastLogAsync("Information", "hello");
        Assert.False(ok);
    }

    [Fact]
    public async Task BroadcastLogAsync_BroadcasterRegistered_CallsServiceAndReturnsTrue()
    {
        var mock = new Mock<IRealtimeBroadcaster>(MockBehavior.Strict);
        _ = mock.Setup(b => b.BroadcastLogAsync("Information", "hello", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sp = new ServiceCollection()
            .AddSingleton(mock.Object)
            .BuildServiceProvider();

        var ctx = NewContext(sp);

        var ok = await ctx.BroadcastLogAsync("Information", "hello");
        Assert.True(ok);

        mock.VerifyAll();
    }

    [Fact]
    public async Task BroadcastLogAsync_BroadcasterThrows_ReturnsFalse()
    {
        var mock = new Mock<IRealtimeBroadcaster>(MockBehavior.Strict);
        _ = mock.Setup(b => b.BroadcastLogAsync("Information", "boom", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));

        var sp = new ServiceCollection()
            .AddSingleton(mock.Object)
            .BuildServiceProvider();

        var ctx = NewContext(sp);

        var ok = await ctx.BroadcastLogAsync("Information", "boom");
        Assert.False(ok);

        mock.VerifyAll();
    }

    [Fact]
    public async Task BroadcastEventAsync_BroadcasterRegistered_CallsServiceAndReturnsTrue()
    {
        var payload = new { a = 1 };

        var mock = new Mock<IRealtimeBroadcaster>(MockBehavior.Strict);
        _ = mock.Setup(b => b.BroadcastEventAsync("evt", payload, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sp = new ServiceCollection()
            .AddSingleton(mock.Object)
            .BuildServiceProvider();

        var ctx = NewContext(sp);

        var ok = await ctx.BroadcastEventAsync("evt", payload);
        Assert.True(ok);

        mock.VerifyAll();
    }

    [Fact]
    public async Task BroadcastToGroupAsync_BroadcasterRegistered_CallsServiceAndReturnsTrue()
    {
        var payload = new { a = 1 };

        var mock = new Mock<IRealtimeBroadcaster>(MockBehavior.Strict);
        _ = mock.Setup(b => b.BroadcastToGroupAsync("group", "method", payload, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sp = new ServiceCollection()
            .AddSingleton(mock.Object)
            .BuildServiceProvider();

        var ctx = NewContext(sp);

        var ok = await ctx.BroadcastToGroupAsync("group", "method", payload);
        Assert.True(ok);

        mock.VerifyAll();
    }
}
