using Microsoft.ClearScript.V8;
using Serilog;
using Serilog.Events; 

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  Python delegate builder  –  now takes optional imports / references
// ---------------------------------------------------------------------------

internal static class JScriptDelegateBuilder
{
    static readonly bool Implemented = false;
    internal static RequestDelegate Build(string code, Serilog.ILogger logger)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Building JavaScript delegate, script length={Length}", code?.Length);
        if (!Implemented)
            throw new NotImplementedException("JavaScript scripting is not yet supported in Kestrun.");
        var engine = new V8ScriptEngine();
        engine.AddHostType("KestrunResponse", typeof(KestrunResponse));
        engine.Execute(code);               // script defines global  function handle(ctx, res) { ... }

        return async context =>
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("JS delegate invoked for {Path}", context.Request.Path);

            var krRequest = await KestrunRequest.NewRequest(context);
            var krResponse = new KestrunResponse(krRequest);
            engine.Script.handle(context, krResponse);

            if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                return;

            await krResponse.ApplyTo(context.Response);
        };
    }
}