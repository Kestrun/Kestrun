using Kestrun.Hosting;
using Kestrun.Languages;
using Microsoft.CodeAnalysis.CSharp;
using Serilog.Events;
using System.Reflection;

namespace Kestrun.Scheduling;

internal static class RoslynJobFactory
{
    public static Func<CancellationToken, Task> Build(
        KestrunHost host,
        string code,
        string[]? extraImports,
        Assembly[]? extraRefs,
        IReadOnlyDictionary<string, object?>? locals,
        LanguageVersion languageVersion = LanguageVersion.CSharp12)
    {
        var log = host.Logger;
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building C# job, code length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        var script = CSharpDelegateBuilder.Compile(host: host, code: code, extraImports: extraImports, extraRefs: extraRefs, locals: locals, languageVersion: languageVersion);
        var runner = script.CreateDelegate();   // returns ScriptRunner<object?>
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("C# job runner created, type={Type}", runner.GetType());
        }
        /* 5️⃣  Returned delegate = *execute only* */
        return async ct =>
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.Debug("Executing C# job at {Now:O}", DateTimeOffset.UtcNow);
            }

            var globals = locals is { Count: > 0 }
                ? new CsGlobals(globals: host.SharedState.Snapshot(), locals: locals)
                : new CsGlobals(globals: host.SharedState.Snapshot());
            _ = await runner(globals, ct).ConfigureAwait(false);
        };
    }



    public static Func<CancellationToken, Task> Build(
        KestrunHost host,
       string code,
       string[]? extraImports,
       Assembly[]? extraRefs,
       IReadOnlyDictionary<string, object?>? locals,
       Microsoft.CodeAnalysis.VisualBasic.LanguageVersion languageVersion = Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9)
    {
        var log = host.Logger;
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building C# job, code length={Length}, imports={ImportsCount}, refs={RefsCount}, lang={Lang}",
                code?.Length, extraImports?.Length ?? 0, extraRefs?.Length ?? 0, languageVersion);
        }

        var script = VBNetDelegateBuilder.Compile<object>(host: host, code: code,
            extraImports: extraImports, extraRefs: extraRefs,
            locals: locals, languageVersion: languageVersion);

        return async ct =>
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.Debug("Executing C# job at {Now:O}", DateTimeOffset.UtcNow);
            }

            var globals = new CsGlobals(globals: host.SharedState.Snapshot());
            _ = await script(globals).ConfigureAwait(false);
        };
    }
}


