using System.Security.Claims;
using System.Text;
using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Logging;
using Kestrun.Models;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis;
using Serilog.Events;
using System.Reflection;

internal static class DelegateBuilder
{
    /// <summary>
    /// Prepares the Kestrun context, response, and script globals for execution.
    /// Encapsulates request parsing, shared state snapshot, arg injection, and logging.
    /// </summary>
    /// <param name="ctx">The current HTTP context.</param>
    /// <param name="log">Logger for diagnostics.</param>
    /// <param name="args">Optional variables to inject into the globals.</param>
    /// <returns>Tuple containing the prepared CsGlobals, KestrunResponse, and KestrunContext.</returns>
    internal static async Task<(CsGlobals Globals, KestrunResponse Response, KestrunContext Context)> PrepareExecutionAsync(
        HttpContext ctx,
        Serilog.ILogger log,
        Dictionary<string, object?>? args)
    {
        var krRequest = await KestrunRequest.NewRequest(ctx).ConfigureAwait(false);
        var krResponse = new KestrunResponse(krRequest);
        var Context = new KestrunContext(krRequest, krResponse, ctx);
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.DebugSanitized("Kestrun context created for {Path}", ctx.Request.Path);
        }

        // Create a shared state dictionary that will be used to store global variables
        // This will be shared across all requests and can be used to store state
        // that needs to persist across multiple requests
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Creating shared state store for Kestrun context");
        }

        var glob = new Dictionary<string, object?>(SharedStateStore.Snapshot());
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Shared state store created with {Count} items", glob.Count);
        }

        // Inject the provided arguments into the globals so the script can access them
        if (args != null && args.Count > 0)
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.Debug("Setting variables from arguments: {Count}", args.Count);
            }

            foreach (var kv in args)
            {
                glob[kv.Key] = kv.Value; // add args to globals
            }
        }

        // Create a new CsGlobals instance with the current context and shared state
        var globals = new CsGlobals(glob, Context);
        return (globals, krResponse, Context);
    }


    /// <summary>
    /// Decides the VB return type string that matches TResult.
    /// </summary>
    /// <param name="ctx">The current HTTP context.</param>
    /// <param name="response">The Kestrun response to apply.</param>
    /// <param name="log">The logger to use for logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task ApplyResponseAsync(HttpContext ctx, KestrunResponse response, Serilog.ILogger log)
    {
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.DebugSanitized("Applying response to Kestrun context for {Path}", ctx.Request.Path);
        }

        if (!string.IsNullOrEmpty(response.RedirectUrl))
        {
            ctx.Response.Redirect(response.RedirectUrl);
            return;
        }

        await response.ApplyTo(ctx.Response).ConfigureAwait(false);

        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.DebugSanitized("Response applied to Kestrun context for {Path}", ctx.Request.Path);
        }
    }

    /// <summary>
    /// Common baseline references shared across script compilations.
    /// </summary>
    /// <returns> MetadataReference[] </returns>
    internal static MetadataReference[] BuildBaselineReferences()
    {
        // Collect unique assembly locations
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(Type t)
        {
            var loc = t.Assembly.Location;
            if (!string.IsNullOrWhiteSpace(loc))
            {
                _ = locations.Add(loc); // capture return to satisfy analyzer
            }
        }

        // Seed core & always-required assemblies
        Add(typeof(object));                                                              // System.Private.CoreLib
        Add(typeof(Enumerable));                                                          // System.Linq
        Add(typeof(HttpContext));                                                         // Microsoft.AspNetCore.Http
        Add(typeof(Console));                                                             // System.Console
        Add(typeof(StringBuilder));                                                       // System.Text
        Add(typeof(Serilog.Log));                                                         // Serilog
        Add(typeof(ClaimsPrincipal));                                                     // System.Security.Claims
        Add(typeof(ClaimsIdentity));                                                      // System.Security.Claims
        Add(typeof(System.Security.Cryptography.X509Certificates.X509Certificate2));      // X509 Certificates

        // Snapshot loaded assemblies once
        var loadedAssemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .ToArray();

        // Attempt to bind each namespace in PlatformImports to a loaded assembly
        foreach (var ns in PlatformImports)
        {
            foreach (var asm in loadedAssemblies)
            {
                if (locations.Contains(asm.Location))
                {
                    continue; // already referenced
                }
                if (NamespaceExistsInAssembly(asm, ns))
                {
                    _ = locations.Add(asm.Location); // capture return to satisfy analyzer
                }
            }
        }

        // Explicitly ensure critical assemblies needed for dynamic auth / razor / encodings even if not yet scanned
        Add(typeof(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults));
        Add(typeof(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults));
        Add(typeof(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults));
        Add(typeof(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults));
        Add(typeof(Microsoft.AspNetCore.Mvc.Razor.RazorPageBase));
        Add(typeof(System.Text.Encodings.Web.HtmlEncoder));

        return [.. locations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(loc => MetadataReference.CreateFromFile(loc))];
    }

    private static bool NamespaceExistsInAssembly(Assembly asm, string @namespace)
    {
        try
        {
            // Using DefinedTypes to avoid loading reflection-only contexts; guard against type load issues.
            foreach (var t in asm.DefinedTypes)
            {
                var ns = t.Namespace;
                if (ns == null)
                {
                    continue;
                }
                if (ns.Equals(@namespace, StringComparison.Ordinal) || ns.StartsWith(@namespace + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // Ignore partially loadable assemblies; namespace absence treated as false.
        }
        return false;
    }

    // Ordered & de-duplicated platform / framework imports used for dynamic script compilation.
    internal static string[] PlatformImports = [
        "Kestrun.Languages",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authentication.Cookies",
        "Microsoft.AspNetCore.Authentication.JwtBearer",
        "Microsoft.AspNetCore.Authentication.Negotiate",
        "Microsoft.AspNetCore.Authentication.OpenIdConnect",
        "Microsoft.AspNetCore.Authorization",
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.RazorPages",
        // "Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation".
        "Microsoft.AspNetCore.Server.Kestrel.Core",
        "Microsoft.AspNetCore.SignalR",
        "Microsoft.CodeAnalysis",
        "Microsoft.CodeAnalysis.CSharp",
        "Microsoft.CodeAnalysis.CSharp.Scripting",
        "Microsoft.CodeAnalysis.Scripting",
        "Microsoft.Extensions.FileProviders",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Primitives",
        "Microsoft.IdentityModel.Tokens",
        "Microsoft.PowerShell",
        "Microsoft.VisualBasic",
        "Serilog",
        "Serilog.Events",
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Collections.Immutable",
        "System.IO",
        "System.Linq",
        "System.Management.Automation",
        "System.Management.Automation.Runspaces",
        "System.Net",
        "System.Net.Http.Headers",
        "System.Reflection",
        "System.Runtime.InteropServices",
        "System.Security.Claims",
        "System.Security.Cryptography.X509Certificates",
        "System.Text",
        "System.Text.Encodings.Web",
        "System.Text.RegularExpressions",
        "System.Threading.Tasks"
    ];
}
