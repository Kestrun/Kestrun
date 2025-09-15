using Kestrun.Models;
using Python.Runtime;
using Serilog.Events;

namespace Kestrun.Languages;
// ---------------------------------------------------------------------------
//  Python delegate builder  –  now takes optional imports / references
// ---------------------------------------------------------------------------

internal static class PyDelegateBuilder
{
    public static bool Implemented { get; set; }
    public static void ConfigurePythonRuntimePath(string path) => Runtime.PythonDLL = path;
    // ---------------------------------------------------------------------------
    //  helpers at class level
    // ---------------------------------------------------------------------------

#if NET9_0_OR_GREATER
    private static readonly Lock _pyGate = new();
#else
    private static readonly object _pyGate = new();
#endif
    private static bool _pyInit;

    private static void EnsurePythonEngine()
    {
        if (_pyInit)
        {
            return;
        }

        lock (_pyGate)
        {
            if (_pyInit)
            {
                return;          // double-check
            }

            // If you need a specific DLL, set Runtime.PythonDLL
            // or expose it via the PYTHONNET_PYDLL environment variable.
            // Runtime.PythonDLL = @"C:\Python312\python312.dll";

            PythonEngine.Initialize();        // load CPython once
            _ = PythonEngine.BeginAllowThreads(); // let other threads run
            _pyInit = true;
        }
    }
    internal static RequestDelegate Build(string code, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(LogEventLevel.Debug))
        {
            logger.Debug("Building Python delegate, script l   ength={Length}", code?.Length);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Python script code cannot be empty.", nameof(code));
        }

        if (!Implemented)
        {
            throw new NotImplementedException("JavaScript scripting is not yet supported in Kestrun.");
        }

        EnsurePythonEngine();                 // one-time init

        // ---------- compile the script once ----------
        using var gil = Py.GIL();           // we are on the caller's thread
        using var scope = Py.CreateScope();

        /*  Expect the user script to contain:

                def handle(ctx, res):
                    # ctx -> ASP.NET HttpContext (proxied)
                    # res -> KestrunResponse    (proxied)
                    ...

            Scope.Exec compiles & executes that code once per route.
        */
        _ = scope.Exec(code);
        dynamic pyHandle = scope.Get("handle");

        // ---------- return a RequestDelegate ----------
        return async context =>
        {
            try
            {
                using var _ = Py.GIL();       // enter GIL for *this* request
                var krRequest = await KestrunRequest.NewRequest(context);
                var krResponse = new KestrunResponse(krRequest);

                // Call the Python handler (Python → .NET marshal is automatic)
                pyHandle(context, krResponse, context);

                // redirect?
                if (!string.IsNullOrEmpty(krResponse.RedirectUrl))
                {
                    context.Response.Redirect(krResponse.RedirectUrl);
                    return;                   // finally-block will CompleteAsync
                }

                // normal response
                await krResponse.ApplyTo(context.Response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // optional logging
                logger.Error($"Python route error: {ex}");
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(
                    "Python script failed while processing the request.").ConfigureAwait(false);
            }
            finally
            {
                // Always flush & close so the client doesn’t hang
                try { await context.Response.CompleteAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { /* client disconnected */ }
            }
        };
    }
}
