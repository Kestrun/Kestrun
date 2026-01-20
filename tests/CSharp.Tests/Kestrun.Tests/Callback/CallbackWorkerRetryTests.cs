using Kestrun.Callback;
using Serilog;
using Xunit;

namespace KestrunTests.Callback;

public class CallbackWorkerRetryTests
{
    static CallbackWorkerRetryTests()
        => Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    public async Task Worker_RetriesOnce_WhenRetryPolicyRequestsRetry()
    {
        var queue = new InMemoryCallbackQueue();

        var sender = new AlwaysFailSender();
        var retry = new RetryOncePolicy();
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

            // Wait until the retry is scheduled and then the second attempt is observed.
            _ = await store.RetryScheduled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _ = await sender.SecondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(2, sender.Calls);
            Assert.Equal(1, store.RetryScheduledCalls);
            Assert.True(req.Attempt >= 1);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed class AlwaysFailSender : ICallbackSender
    {
        public int Calls { get; private set; }

        public TaskCompletionSource<bool> SecondAttempt { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CallbackResult> SendAsync(CallbackRequest request, CancellationToken ct)
        {
            Calls++;
            if (Calls >= 2)
            {
                _ = SecondAttempt.TrySetResult(true);
            }

            return Task.FromResult(new CallbackResult(false, 500, "HttpError", "err", DateTimeOffset.UtcNow));
        }
    }

    private sealed class RetryOncePolicy : ICallbackRetryPolicy
    {
        public RetryDecision Evaluate(CallbackRequest req, CallbackResult result)
            => req.Attempt == 0
                ? new RetryDecision(
                    RetryDecisionKind.Retry,
                    DateTimeOffset.UtcNow,
                    TimeSpan.Zero,
                    "RetryOnce")
                : new RetryDecision(
                    RetryDecisionKind.Stop,
                    req.NextAttemptAt,
                    TimeSpan.Zero,
                    "Stop");
    }

    private sealed class CapturingStore : ICallbackStore
    {
        public int RetryScheduledCalls { get; private set; }

        public TaskCompletionSource<bool> RetryScheduled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SaveNewAsync(CallbackRequest req, CancellationToken ct) => Task.CompletedTask;

        public Task MarkInFlightAsync(CallbackRequest req, CancellationToken ct) => Task.CompletedTask;

        public Task MarkSucceededAsync(CallbackRequest req, CallbackResult res, CancellationToken ct) => Task.CompletedTask;

        public Task MarkRetryScheduledAsync(CallbackRequest req, CallbackResult res, CancellationToken ct)
        {
            RetryScheduledCalls++;
            _ = RetryScheduled.TrySetResult(true);
            return Task.CompletedTask;
        }

        public Task MarkFailedPermanentAsync(CallbackRequest req, CallbackResult res, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<CallbackRequest>> DequeueDueAsync(int max, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CallbackRequest>>([]);
    }
}
