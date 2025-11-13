using System.Text.Encodings.Web;
using Kestrun.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using Xunit;
using Kestrun.Hosting;

namespace KestrunTests.Authentication;

public class ApiKeyAuthHandlerWhitespaceTests
{
    private static ApiKeyAuthenticationOptions MakeOptions()
    {
        return new ApiKeyAuthenticationOptions
        {
            HeaderName = "X-Api-Key",
            AdditionalHeaderNames = ["X-Alt-Api-Key"],
            AllowQueryStringFallback = true,
            RequireHttps = false,
            EmitChallengeHeader = false,
            ExpectedKey = "my-secret-api-key",
            Logger = new LoggerConfiguration().MinimumLevel.Error().CreateLogger()
        };
    }

    private static async Task<TestApiKeyHandler> MakeHandlerAsync(ApiKeyAuthenticationOptions opts, DefaultHttpContext ctx)
    {
        IOptionsMonitor<ApiKeyAuthenticationOptions> monitor = new StaticOptionsMonitor<ApiKeyAuthenticationOptions>(opts);
        var loggerFactory = LoggerFactory.Create(_ => { });
        var hostLogger = opts.Logger ?? new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("Tests", hostLogger);
        var handler = new TestApiKeyHandler(host, monitor, loggerFactory, UrlEncoder.Default);
        var scheme = new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthHandler));
        await handler.InitializeAsync(scheme, ctx);
        return handler;
    }

    [Fact]
    public async Task Header_with_trailing_whitespace_authenticates()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        var opts = MakeOptions();
        ctx.Request.Headers[opts.HeaderName] = "my-secret-api-key\r\n ";

        var handler = await MakeHandlerAsync(opts, ctx);
        var result = await handler.PublicHandleAuthenticateAsync();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Additional_header_with_whitespace_authenticates()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        var opts = MakeOptions();
        ctx.Request.Headers["X-Alt-Api-Key"] = " my-secret-api-key\n";

        var handler = await MakeHandlerAsync(opts, ctx);
        var result = await handler.PublicHandleAuthenticateAsync();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Query_string_with_encoded_spaces_authenticates_when_fallback_enabled()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        var opts = MakeOptions();
        ctx.Request.QueryString = new QueryString("?X-Api-Key=%20my-secret-api-key%20%0D%0A");

        var handler = await MakeHandlerAsync(opts, ctx);
        var result = await handler.PublicHandleAuthenticateAsync();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Success_emits_no_warning_logs()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        var opts = MakeOptions();
        opts.Logger = logger;
        ctx.Request.Headers[opts.HeaderName] = "my-secret-api-key \r\n";

        var handler = await MakeHandlerAsync(opts, ctx);
        var result = await handler.PublicHandleAuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    [Fact]
    public async Task Wrong_key_fails_and_emits_warning()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        var opts = MakeOptions();
        opts.Logger = logger;
        ctx.Request.Headers[opts.HeaderName] = "not-the-key\n";

        var handler = await MakeHandlerAsync(opts, ctx);
        var result = await handler.PublicHandleAuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    [Fact]
    public async Task Query_string_wrong_key_with_whitespace_fails_and_logs_reason()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        var opts = MakeOptions();
        opts.Logger = logger;
        // Encoded spaces + CRLF around wrong key
        ctx.Request.QueryString = new QueryString("?X-Api-Key=%20not-the-key%20%0D%0A");

        var handler = await MakeHandlerAsync(opts, ctx);
        var result = await handler.PublicHandleAuthenticateAsync();

        Assert.False(result.Succeeded);
        var warn = sink.Events.FirstOrDefault(e => e.Level == LogEventLevel.Warning);
        Assert.NotNull(warn);
        Assert.True(warn.Properties.TryGetValue("Reason", out var reason));
        var reasonText = (reason as ScalarValue)?.Value?.ToString();
        Assert.Equal("Invalid API Key: not-the-key", reasonText);
    }

    private sealed class TestApiKeyHandler(KestrunHost host, IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder) : ApiKeyAuthHandler(host, options, logger, encoder)
    {
        public Task<AuthenticateResult> PublicHandleAuthenticateAsync() => HandleAuthenticateAsync();
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public ConcurrentBag<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
