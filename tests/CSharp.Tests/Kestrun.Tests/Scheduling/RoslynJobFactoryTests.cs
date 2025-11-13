using Kestrun.Scheduling;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using Microsoft.CodeAnalysis.Scripting;
using Kestrun.Hosting;

namespace KestrunTests.Scheduling;

public class RoslynJobFactoryTests
{
    private sealed class CollectSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (ILogger Logger, CollectSink Sink) MakeLogger()
    {
        var sink = new CollectSink();
        var log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (log, sink);
    }


    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_ExtraImports_Runs()
    {
        var host = new KestrunHost("TestHost");
        // Use fully-qualified type name to avoid relying on import resolution in case of namespace issues
        var code = "var sb = new System.Text.StringBuilder(); sb.Append(\"hi\");";
        var job = RoslynJobFactory.Build(host, code, ["System.Text"], null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task VB_Build_Runs_Trivial_Code()
    {
        var host = new KestrunHost("TestHost");
        var job = RoslynJobFactory.Build(host, "Return True", null, null, null, Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9);
        await job(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_Locals_Injection_Works()
    {
        var host = new KestrunHost("TestHost");
        var locals = new Dictionary<string, object?> { ["foo"] = "bar" };
        var job = RoslynJobFactory.Build(host, "if(foo != \"bar\") throw new System.Exception(\"locals not injected\");", null, null, locals, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None); // should not throw
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_Global_Injection_Works()
    {
        var host = new KestrunHost("TestHost");
        _ = host.SharedState.Set("testGlobalGreeting", "hello-world");
        var job = RoslynJobFactory.Build(host, "if(testGlobalGreeting != \"hello-world\") throw new System.Exception(\"global missing\");", null, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None); // should not throw
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void CSharp_Build_Invalid_Code_Throws_With_Diagnostics()
    {
        var (log, sink) = MakeLogger();
        var host = new KestrunHost("TestHost", logger: log);
        var ex = Assert.Throws<CompilationErrorException>(() => RoslynJobFactory.Build(host, "var x = ;", null, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12));
        Assert.Contains("C# script compilation completed with", ex.Message);
        // Also ensure an error was logged
        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains("Error [CS"));
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_Generic_Global_Type_Compiles()
    {
        var (log, _) = MakeLogger();
        var host = new KestrunHost("TestHost", logger: log);
        _ = host.SharedState.Set("myDict", new Dictionary<string, object?>());
        var job = RoslynJobFactory.Build(host, "myDict[\"k\"] = \"v\";", null, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None); // if generic formatting failed, compilation would throw earlier
    }
}
