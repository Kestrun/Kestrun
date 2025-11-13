using Kestrun.Hosting;
using Serilog.Events;

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  Python delegate builder  –  now takes optional imports / references
// ---------------------------------------------------------------------------

internal static class FSharpDelegateBuilder
{
    internal static RequestDelegate Build(KestrunHost host, string code)
    {
        var logger = host.Logger;
        // F# scripting not implemented yet
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("Building F# delegate, script length={Length}", code?.Length);
        }

        throw new NotImplementedException("F# scripting is not yet supported in Kestrun.");
    }
}
