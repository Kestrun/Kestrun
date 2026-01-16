using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using Serilog;
using Xunit;
using Xunit.Sdk;

namespace KestrunTests.Sse;

public class InMemorySseBroadcasterTests
{
    [Trait("Category", "Sse")]
    [Fact]
    public void Subscribe_IncrementsConnectedCount_AndReturnsUniqueClientIds()
    {
        var broadcaster = new Kestrun.Sse.InMemorySseBroadcaster(CreateNullLogger());

        var sub1 = broadcaster.Subscribe(CancellationToken.None);
        var sub2 = broadcaster.Subscribe(CancellationToken.None);

        Assert.Equal(2, broadcaster.ConnectedCount);
        Assert.False(string.IsNullOrWhiteSpace(sub1.ClientId));
        Assert.False(string.IsNullOrWhiteSpace(sub2.ClientId));
        Assert.NotEqual(sub1.ClientId, sub2.ClientId);
    }

    [Trait("Category", "Sse")]
    [Fact]
    public async Task BroadcastAsync_WritesFormattedPayloadToAllSubscribers()
    {
        var broadcaster = new Kestrun.Sse.InMemorySseBroadcaster(CreateNullLogger());
        var sub1 = broadcaster.Subscribe(CancellationToken.None);
        var sub2 = broadcaster.Subscribe(CancellationToken.None);

        await broadcaster.BroadcastAsync(eventName: "message", data: "hello", id: "42", retryMs: 1000);

        var expected = Kestrun.Sse.SseEventFormatter.Format("message", "hello", "42", 1000);
        var p1 = await ReadWithTimeoutAsync(sub1.Reader, TimeSpan.FromSeconds(2));
        var p2 = await ReadWithTimeoutAsync(sub2.Reader, TimeSpan.FromSeconds(2));

        Assert.Equal(expected, p1);
        Assert.Equal(expected, p2);
    }

    [Trait("Category", "Sse")]
    [Fact]
    public async Task BroadcastAsync_WhenNoClients_DoesNotThrow()
    {
        var broadcaster = new Kestrun.Sse.InMemorySseBroadcaster(CreateNullLogger());

        await broadcaster.BroadcastAsync(eventName: "message", data: "hello");

        Assert.Equal(0, broadcaster.ConnectedCount);
    }

    [Trait("Category", "Sse")]
    [Fact]
    public async Task Subscribe_WhenCanceled_RemovesClientAndCompletesReader()
    {
        var broadcaster = new Kestrun.Sse.InMemorySseBroadcaster(CreateNullLogger());
        using var cts = new CancellationTokenSource();

        var sub = broadcaster.Subscribe(cts.Token);
        Assert.Equal(1, broadcaster.ConnectedCount);

        cts.Cancel();

        var removed = await WaitForAsync(() => broadcaster.ConnectedCount == 0, TimeSpan.FromSeconds(2));
        Assert.True(removed);

        await AssertCompletesAsync(sub.Reader, TimeSpan.FromSeconds(2));
    }

    [Trait("Category", "Sse")]
    [Fact]
    public async Task BroadcastAsync_WhenCanceledToken_DoesNotWrite()
    {
        var broadcaster = new Kestrun.Sse.InMemorySseBroadcaster(CreateNullLogger());
        var sub = broadcaster.Subscribe(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await broadcaster.BroadcastAsync(eventName: "x", data: "y", cancellationToken: cts.Token);

        Assert.False(sub.Reader.TryRead(out _));
    }

    [Trait("Category", "Sse")]
    [Fact]
    public async Task BroadcastAsync_WhenClientChannelCompleted_RemovesClient()
    {
        var broadcaster = new Kestrun.Sse.InMemorySseBroadcaster(CreateNullLogger());
        var sub = broadcaster.Subscribe(CancellationToken.None);
        Assert.Equal(1, broadcaster.ConnectedCount);

        // Simulate a broken client channel without removing it from the broadcaster.
        // This forces Writer.TryWrite(payload) to fail so the broadcaster removes the client.
        var clients = GetClientDictionary(broadcaster);
        Assert.True(clients.TryGetValue(sub.ClientId, out var channel));
        _ = channel.Writer.TryComplete();

        await broadcaster.BroadcastAsync(eventName: "x", data: "y");

        var removed = await WaitForAsync(() => broadcaster.ConnectedCount == 0, TimeSpan.FromSeconds(2));
        Assert.True(removed);
    }

    private static ILogger CreateNullLogger() =>
        new LoggerConfiguration().MinimumLevel.Verbose().CreateLogger();

    private static ConcurrentDictionary<string, Channel<string>> GetClientDictionary(Kestrun.Sse.InMemorySseBroadcaster broadcaster)
    {
        var field = typeof(Kestrun.Sse.InMemorySseBroadcaster)
            .GetField("_clients", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var value = field.GetValue(broadcaster);
        Assert.NotNull(value);

        return (ConcurrentDictionary<string, Channel<string>>)value;
    }

    private static async Task<string> ReadWithTimeoutAsync(ChannelReader<string> reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new XunitException($"Timed out waiting for SSE payload after {timeout}.");
        }
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }

    private static async Task AssertCompletesAsync(ChannelReader<string> reader, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(reader.Completion, Task.Delay(timeout)) == reader.Completion;
        Assert.True(completed);
    }
}
