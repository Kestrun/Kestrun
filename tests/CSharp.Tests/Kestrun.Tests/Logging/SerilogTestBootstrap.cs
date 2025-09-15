using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace KestrunTests.Logging;

// Ensures Serilog is configured at Debug level for the entire C# test run,
// so code guarded by Log.IsEnabled(LogEventLevel.Debug) is covered.
internal static class SerilogTestBootstrap
{
    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) { /* discard */ }
    }

    [ModuleInitializer]
    internal static void Init()
    {
        // If a test wants a custom logger, it can overwrite Log.Logger and restore it.
        // Default here is Debug level with a no-op sink to avoid noisy output in CI.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new NullSink())
            .CreateLogger();
    }
}
