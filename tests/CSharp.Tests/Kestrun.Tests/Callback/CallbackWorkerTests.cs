using Kestrun.Callback;
using Serilog;
using Xunit;

namespace KestrunTests.Callback;

public class CallbackWorkerTests
{
    static CallbackWorkerTests()
        => Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    public async Task Worker_ProcessesRequest_AndMarksStoreSucceeded_OnSuccess()
    {
        var queue = new InMemoryCallbackQueue();

        var sender = new FakeSender(new CallbackResult(true, 200, null, null, DateTimeOffset.UtcNow));
        var retry = new FakeRetryPolicy();
        var store = new CapturingStore();

        var worker = new CallbackWorker(queue, sender, retry, Log.Logger, store);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var req = new CallbackRequest(
                callbackId: "cb",
                operationId: "op",
                targetUrl: new Uri("https://example.com/cb"),
                httpMethod: "POST",
                headers: new Dictionary<string, string>(),
                contentType: "application/json",
                body: null,
                correlationId: "cid",
                idempotencyKey: "idk",
                timeout: TimeSpan.FromSeconds(1));

            await queue.Channel.Writer.WriteAsync(req);

            // Wait until MarkSucceededAsync runs.
            _ = await store.Succeeded.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, sender.Calls);
            Assert.Equal(1, store.InFlightCalls);
            Assert.Equal(1, store.SucceededCalls);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed class FakeSender(CallbackResult result) : ICallbackSender
    {
        private readonly CallbackResult _result = result;

        public int Calls { get; private set; }

        public Task<CallbackResult> SendAsync(CallbackRequest request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeRetryPolicy : ICallbackRetryPolicy
    {
        public RetryDecision Evaluate(CallbackRequest req, CallbackResult result)
            => new(RetryDecisionKind.Stop, req.NextAttemptAt, TimeSpan.Zero, "Stop");
    }

    private sealed class CapturingStore : ICallbackStore
    {
        public TaskCompletionSource<bool> Succeeded { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InFlightCalls { get; private set; }
        public int SucceededCalls { get; private set; }

        public Task SaveNewAsync(CallbackRequest req, CancellationToken ct) => Task.CompletedTask;

        public Task MarkInFlightAsync(CallbackRequest req, CancellationToken ct)
        {
            InFlightCalls++;
            return Task.CompletedTask;
        }

        public Task MarkSucceededAsync(CallbackRequest req, CallbackResult res, CancellationToken ct)
        {
            SucceededCalls++;
            _ = Succeeded.TrySetResult(true);
            return Task.CompletedTask;
        }

        public Task MarkRetryScheduledAsync(CallbackRequest req, CallbackResult res, CancellationToken ct) => Task.CompletedTask;

        public Task MarkFailedPermanentAsync(CallbackRequest req, CallbackResult res, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<CallbackRequest>> DequeueDueAsync(int max, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CallbackRequest>>([]);
    }
}
