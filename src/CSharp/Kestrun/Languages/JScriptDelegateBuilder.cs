using Kestrun.Hosting;
using Kestrun.Models;
using Microsoft.ClearScript.V8;
using Serilog.Events;

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  Python delegate builder  –  now takes optional imports / references
// ---------------------------------------------------------------------------

internal static class JScriptDelegateBuilder
{
    public static bool Implemented { get; set; }
    internal static RequestDelegate Build(KestrunHost host, string code)
    {
        var logger = host.Logger;
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("Building JavaScript delegate, script length={Length}", code?.Length);
        }

        if (!Implemented)
        {
            throw new NotImplementedException("JavaScript scripting is not yet supported in Kestrun.");
        }

        var engine = new V8ScriptEngine();
        engine.AddHostType("KestrunResponse", typeof(KestrunResponse));
        engine.Execute(code);               // script defines global  function handle(ctx, res) { ... }

        return async context =>
        {
            var krRequest = await KestrunRequest.NewRequest(context);
            var krResponse = new KestrunResponse(krRequest);
            engine.Script.handle(context, krResponse);

            if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
            {
                return;
            }

            await krResponse.ApplyTo(context.Response);
        };
    }
}
