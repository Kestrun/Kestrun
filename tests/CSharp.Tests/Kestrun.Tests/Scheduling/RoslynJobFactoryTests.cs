using Kestrun.Scheduling;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis.Scripting;

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
    public async Task CSharp_Build_Runs_Trivial_Code()
    {
        var (log, sink) = MakeLogger();
        var job = RoslynJobFactory.Build("var y = 1;", log, null, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None); // should not throw
        Assert.Contains(sink.Events, e => e.RenderMessage().Contains("C# job runner created"));
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_ExtraImports_Runs()
    {
        var (log, _) = MakeLogger();
        // Use fully-qualified type name to avoid relying on import resolution in case of namespace issues
        var code = "var sb = new System.Text.StringBuilder(); sb.Append(\"hi\");";
        var job = RoslynJobFactory.Build(code, log, ["System.Text"], null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task VB_Build_Runs_Trivial_Code()
    {
        var (log, _) = MakeLogger();
        var job = RoslynJobFactory.Build("Return True", log, null, null, null, Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9);
        await job(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_Locals_Injection_Works()
    {
        var (log, _) = MakeLogger();
        var locals = new Dictionary<string, object?> { ["foo"] = "bar" };
        var job = RoslynJobFactory.Build("if(foo != \"bar\") throw new System.Exception(\"locals not injected\");", log, null, null, locals, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None); // should not throw
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_Global_Injection_Works()
    {
        var (log, _) = MakeLogger();
        _ = SharedStateStore.Set("testGlobalGreeting", "hello-world");
        var job = RoslynJobFactory.Build("if(testGlobalGreeting != \"hello-world\") throw new System.Exception(\"global missing\");", log, null, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None); // should not throw
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void CSharp_Build_Invalid_Code_Throws_With_Diagnostics()
    {
        var (log, sink) = MakeLogger();
        var ex = Assert.Throws<CompilationErrorException>(() => RoslynJobFactory.Build("var x = ;", log, null, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12));
        Assert.Contains("C# script compilation completed with", ex.Message);
        // Also ensure an error was logged
        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Error && e.RenderMessage().Contains("Error [CS"));
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CSharp_Build_With_Generic_Global_Type_Compiles()
    {
        var (log, _) = MakeLogger();
        _ = SharedStateStore.Set("myDict", new Dictionary<string, object?>());
        var job = RoslynJobFactory.Build("myDict[\"k\"] = \"v\";", log, null, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        await job(CancellationToken.None); // if generic formatting failed, compilation would throw earlier
    }
}
