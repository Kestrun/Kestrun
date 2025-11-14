using System.Net;
using System.Text.RegularExpressions;
using Kestrun.Hosting.Options;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.Runtime;
using Kestrun.Scripting;
using Kestrun.TBuilder;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Serilog.Events;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for mapping routes and handlers to the KestrunHost.
/// </summary>
public static partial class KestrunHostMapExtensions
{
    /// <summary>
    /// Public utility facade for endpoint specification parsing. This provides a stable API surface
    /// over the internal helper logic used by host route constraint processing.
    /// </summary>
    public static class EndpointSpecParser
    {
        /// <summary>
        /// Parses an endpoint specification into host, port and optional HTTPS flag.
        /// </summary>
        /// <param name="spec">Specification string. See <see cref="TryParseEndpointSpec"/> for accepted formats.</param>
        /// <param name="host">Resolved host when successful, otherwise empty string.</param>
        /// <param name="port">Resolved port when successful, otherwise 0.</param>
        /// <param name="https">True for https, false for http, null when unspecified (host:port form).</param>
        /// <returns><c>true</c> if parsing succeeds; otherwise <c>false</c>.</returns>
        public static bool TryParse(string spec, out string host, out int port, out bool? https)
            => TryParseEndpointSpec(spec, out host, out port, out https);
    }
    /// <summary>
    /// Represents a delegate that handles a Kestrun request with the provided context.
    /// </summary>
    /// <param name="Context">The context for the Kestrun request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public delegate Task KestrunHandler(KestrunContext Context);

    /// <summary>
    /// Adds a native route to the KestrunHost for the specified pattern and HTTP verb.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerb">The HTTP verb for the route.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <param name="map">The endpoint convention builder for further configuration.</param>
    /// <returns>The KestrunHost instance for chaining.</returns>
    public static KestrunHost AddMapRoute(this KestrunHost host, string pattern, HttpVerb httpVerb, KestrunHandler handler, out IEndpointConventionBuilder? map, string[]? requireSchemes = null) =>
    host.AddMapRoute(pattern: pattern, httpVerbs: [httpVerb], handler: handler, out map, requireSchemes: requireSchemes);

    /// <summary>
    /// Adds a native route to the KestrunHost for the specified pattern and HTTP verbs.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerbs">The HTTP verbs for the route.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <param name="map">The endpoint convention builder for further configuration.</param>
    /// <returns>The KestrunHost instance for chaining.</returns>
    public static KestrunHost AddMapRoute(this KestrunHost host, string pattern, IEnumerable<HttpVerb> httpVerbs, KestrunHandler handler,
    out IEndpointConventionBuilder? map, string[]? requireSchemes = null)
    {
        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [.. httpVerbs],
            ScriptCode = new LanguageOptions
            {
                Language = ScriptLanguage.Native,
            },
            RequireSchemes = requireSchemes ?? [] // No authorization by default
        }, handler, out map);
    }

    /// <summary>
    /// Adds a native route to the KestrunHost using the specified MapRouteOptions and handler.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="map">The endpoint convention builder for further configuration.</param>
    /// <returns>The KestrunHost instance for chaining.</returns>
    public static KestrunHost AddMapRoute(this KestrunHost host, MapRouteOptions options, KestrunHandler handler, out IEndpointConventionBuilder? map)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("AddMapRoute called with options={Options}", options);
        }
        // Ensure the WebApplication is initialized
        if (host.App is null)
        {
            throw new InvalidOperationException("WebApplication is not initialized. Call EnableConfiguration first.");
        }

        // Validate options
        if (string.IsNullOrWhiteSpace(options.Pattern))
        {
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));
        }

        string[] methods = [.. options.HttpVerbs.Select(v => v.ToMethodString())];
        map = host.App.MapMethods(options.Pattern, methods, async context =>
         {
             // 🔒 CSRF validation only for the current request when that verb is unsafe (unless disabled)
             if (ShouldValidateCsrf(options, context))
             {
                 if (!await TryValidateAntiforgeryAsync(context))
                 {
                     return; // already responded 400
                 }
             }
             var req = await KestrunRequest.NewRequest(context);
             var res = new KestrunResponse(req);
             KestrunContext kestrunContext = new(host, req, res, context);
             await handler(kestrunContext);
             await res.ApplyTo(context.Response);
         });

        host.AddMapOptions(map, options);

        host.Logger.Information("Added native route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));
        // Add to the feature queue for later processing
        host.FeatureQueue.Add(host => host.AddMapRoute(options));
        return host;
    }


    /// <summary>
    /// Adds a route to the KestrunHost that executes a script block for the specified HTTP verb and pattern.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerbs">The HTTP verb for the route.</param>
    /// <param name="scriptBlock">The script block to execute.</param>
    /// <param name="language">The scripting language to use (default is PowerShell).</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <param name="arguments">Optional dictionary of arguments to pass to the script.</param>
    /// <returns>The KestrunHost instance for chaining.</returns>
    public static KestrunHost AddMapRoute(this KestrunHost host, string pattern, HttpVerb httpVerbs, string scriptBlock, ScriptLanguage language = ScriptLanguage.PowerShell,
                                     string[]? requireSchemes = null,
                                 Dictionary<string, object?>? arguments = null)
    {
        arguments ??= [];
        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [httpVerbs],
            ScriptCode = new LanguageOptions
            {
                Code = scriptBlock,
                Language = language,
                Arguments = arguments ?? [] // No additional arguments by default
            },
            RequireSchemes = requireSchemes ?? [], // No authorization by default
        });
    }

    /// <summary>
    /// Adds a route to the KestrunHost that executes a script block for the specified HTTP verbs and pattern.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerbs">The HTTP verbs for the route.</param>
    /// <param name="scriptBlock">The script block to execute.</param>
    /// <param name="language">The scripting language to use (default is PowerShell).</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <param name="arguments">Optional dictionary of arguments to pass to the script.</param>
    /// <returns>The KestrunHost instance for chaining.</returns>
    public static KestrunHost AddMapRoute(this KestrunHost host, string pattern,
                                IEnumerable<HttpVerb> httpVerbs,
                                string scriptBlock,
                                ScriptLanguage language = ScriptLanguage.PowerShell,
                                string[]? requireSchemes = null,
                                 Dictionary<string, object?>? arguments = null)
    {
        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [.. httpVerbs],
            ScriptCode = new LanguageOptions
            {
                Code = scriptBlock,
                Language = language,
                Arguments = arguments ?? [] // No additional arguments by default
            },
            RequireSchemes = requireSchemes ?? [], // No authorization by default
        });
    }

    /// <summary>
    /// Adds a route to the KestrunHost using the specified MapRouteOptions.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <returns>The KestrunHost instance for chaining.</returns>
    public static KestrunHost AddMapRoute(this KestrunHost host, MapRouteOptions options)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("AddMapRoute called with pattern={Pattern}, language={Language}, method={Methods}", options.Pattern, options.ScriptCode.Language, options.HttpVerbs);
        }
        if (host.IsConfigured)
        {
            _ = CreateMapRoute(host, options);
        }
        else
        {
            _ = host.Use(app =>
            {
                _ = CreateMapRoute(host, options);
            });
        }
        return host; // for chaining
    }

    /// <summary>
    /// Adds a route to the KestrunHost using the specified MapRouteOptions.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <returns>The IEndpointConventionBuilder for the created route.</returns>
    private static IEndpointConventionBuilder CreateMapRoute(KestrunHost host, MapRouteOptions options)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("AddMapRoute called with pattern={Pattern}, language={Language}, method={Methods}", options.Pattern, options.ScriptCode.Language, options.HttpVerbs);
        }

        try
        {
            // Validate options and get normalized route options
            if (!ValidateRouteOptions(host, options, out var routeOptions))
            {
                return null!; // Route already exists and should be skipped
            }

            var logger = host.Logger.ForContext("Route", routeOptions.Pattern);

            // Compile the script once – return a RequestDelegate
            var compiled = CompileScript(host, options.ScriptCode);

            // Create and register the route
            return CreateAndRegisterRoute(host, routeOptions, compiled);
        }
        catch (CompilationErrorException ex)
        {
            // Log the detailed compilation errors
            host.Logger.Error($"Failed to add route '{options.Pattern}' due to compilation errors:");
            host.Logger.Error(ex.GetDetailedErrorMessage());

            // Re-throw with additional context
            throw new InvalidOperationException(
                $"Failed to compile {options.ScriptCode.Language} script for route '{options.Pattern}'. {ex.GetErrors().Count()} error(s) found.",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to add route '{options.Pattern}' with method '{string.Join(", ", options.HttpVerbs)}' using {options.ScriptCode.Language}: {ex.Message}",
                ex);
        }
    }


    /// <summary>
    /// Validates the host and options for adding a map route.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions to validate.</param>
    /// <param name="routeOptions">The validated route options with defaults applied.</param>
    /// <returns>True if validation passes and route should be added; false if duplicate route should be skipped.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WebApplication is not initialized or route already exists and ThrowOnDuplicate is true.</exception>
    /// <exception cref="ArgumentException">Thrown when required options are invalid.</exception>
    internal static bool ValidateRouteOptions(KestrunHost host, MapRouteOptions options, out MapRouteOptions routeOptions)
    {
        // Ensure the WebApplication is initialized
        if (host.App is null)
        {
            throw new InvalidOperationException("WebApplication is not initialized. Call EnableConfiguration first.");
        }

        // Validate options
        if (string.IsNullOrWhiteSpace(options.Pattern))
        {
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));
        }

        // Validate code
        if (string.IsNullOrWhiteSpace(options.ScriptCode.Code))
        {
            throw new ArgumentException("ScriptBlock cannot be null or empty.", nameof(options.ScriptCode.Code));
        }

        routeOptions = options;
        if (options.HttpVerbs.Count == 0)
        {
            // Create a new RouteOptions with HttpVerbs set to [HttpVerb.Get]
            routeOptions = options with { HttpVerbs = [HttpVerb.Get] };
        }

        if (MapExists(host, routeOptions.Pattern, routeOptions.HttpVerbs))
        {
            var msg = $"Route '{routeOptions.Pattern}' with method(s) {string.Join(", ", routeOptions.HttpVerbs)} already exists.";
            if (options.ThrowOnDuplicate)
            {
                throw new InvalidOperationException(msg);
            }

            host.Logger.Warning(msg);
            return false; // Skip this route
        }

        return true; // Continue with route creation
    }

    /// <summary>
    /// Compiles the script code for the specified language.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The language options containing the script code and language.</param>
    /// <returns>A compiled RequestDelegate that can handle HTTP requests.</returns>
    /// <exception cref="NotSupportedException">Thrown when the script language is not supported.</exception>
    internal static RequestDelegate CompileScript(this KestrunHost host, LanguageOptions options)
    {
        return options.Language switch
        {
            ScriptLanguage.PowerShell => PowerShellDelegateBuilder.Build(host, options.Code!, options.Arguments),
            ScriptLanguage.CSharp => CSharpDelegateBuilder.Build(host, options.Code!, options.Arguments, options.ExtraImports, options.ExtraRefs),
            ScriptLanguage.VBNet => VBNetDelegateBuilder.Build(host, options.Code!, options.Arguments, options.ExtraImports, options.ExtraRefs),
            ScriptLanguage.FSharp => FSharpDelegateBuilder.Build(host, options.Code!), // F# scripting not implemented
            ScriptLanguage.Python => PyDelegateBuilder.Build(host, options.Code!),
            ScriptLanguage.JavaScript => JScriptDelegateBuilder.Build(host, options.Code!),
            _ => throw new NotSupportedException(options.Language.ToString())
        };
    }

    /// <summary>
    /// Creates and registers a route with the specified options and compiled handler.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="routeOptions">The validated route options.</param>
    /// <param name="compiled">The compiled script delegate.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    internal static IEndpointConventionBuilder CreateAndRegisterRoute(KestrunHost host, MapRouteOptions routeOptions, RequestDelegate compiled)
    {
        // Wrap with CSRF validation
        async Task handler(HttpContext ctx)
        {
            if (ShouldValidateCsrf(routeOptions, ctx))
            {
                if (!await TryValidateAntiforgeryAsync(ctx))
                {
                    return; // already responded 400
                }
            }
            await compiled(ctx);
        }

        string[] methods = [.. routeOptions.HttpVerbs.Select(v => v.ToMethodString())];
        var map = host.App!.MapMethods(routeOptions.Pattern!, methods, handler).WithLanguage(routeOptions.ScriptCode.Language);

        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Mapped route: {Pattern} with methods: {Methods}", routeOptions.Pattern, string.Join(", ", methods));
        }

        host.AddMapOptions(map, routeOptions);

        foreach (var method in routeOptions.HttpVerbs.Select(v => v.ToMethodString()))
        {
            host._registeredRoutes[(routeOptions.Pattern!, method)] = routeOptions;
        }

        host.Logger.Information("Added route: {Pattern} with methods: {Methods}", routeOptions.Pattern, string.Join(", ", methods));
        return map;
    }

    /// <summary>
    /// Adds additional mapping options to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    internal static void AddMapOptions(this KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        ApplyShortCircuit(host, map, options);
        ApplyAnonymous(host, map, options);
        DisableAntiforgery(host, map, options);
        DisableResponseCompression(host, map, options);
        ApplyRateLimiting(host, map, options);
        ApplyAuthSchemes(host, map, options);
        ApplyPolicies(host, map, options);
        ApplyCors(host, map, options);
        ApplyOpenApiMetadata(host, map, options);
        ApplyRequiredHost(host, map, options);
    }

    /// <summary>
    /// Tries to parse an endpoint specification string into its components: host, port, and HTTPS flag.
    /// </summary>
    /// <param name="spec">The endpoint specification string.</param>
    /// <param name="host">The host component.</param>
    /// <param name="port">The port component.</param>
    /// <param name="https">
    /// Indicates HTTPS (<c>true</c>) or HTTP (<c>false</c>) when the scheme is explicitly specified via a full URL.
    /// For host:port forms where no scheme information is available the value is <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if parsing succeeds; otherwise <c>false</c> and <paramref name="host"/> will be <c>string.Empty</c> and <paramref name="port"/> <c>0</c>.
    /// </returns>
    /// <remarks>
    /// Accepted formats (in priority order):
    /// <list type="bullet">
    /// <item><description>Full URL: <c>https://host:port</c>, <c>http://host:port</c>, IPv6 literal allowed in brackets; if the port is omitted the default (80/443) is inferred.</description></item>
    /// <item><description>Bracketed IPv6 host &amp; port: <c>[::1]:5000</c>, <c>[2001:db8::1]:8080</c>.</description></item>
    /// <item><description>Host or IPv4 with port: <c>localhost:5000</c>, <c>127.0.0.1:8080</c>, <c>example.com:443</c>.</description></item>
    /// </list>
    /// Unsupported / rejected examples: non http(s) schemes (e.g. <c>ftp://</c>), missing port in host:port form, empty port (<c>https://localhost:</c>), malformed IPv6 without brackets.
    /// </remarks>
    public static bool TryParseEndpointSpec(string spec, out string host, out int port, out bool? https)
    {
        host = ""; port = 0; https = null;

        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        // 1. Try full URL form first
        if (TryParseUrlSpec(spec, out host, out port, out https))
        {
            return true;
        }

        // 2. Bracketed IPv6 literal with port: [::1]:5000
        if (TryParseBracketedIpv6Spec(spec, out host, out port))
        {
            return true; // https stays null (not specified)
        }

        // 3. Regular host:port (hostname, IPv4, or raw IPv6 w/out brackets not supported here)
        if (TryParseHostPortSpec(spec, out host, out port))
        {
            return true; // https stays null (not specified)
        }

        // No match
        host = ""; port = 0; https = null;
        return false;
    }

    /// <summary>
    /// Tries to parse a full URL endpoint specification.
    /// </summary>
    /// <param name="spec">The endpoint specification string.</param>
    /// <param name="host">The parsed host component.</param>
    /// <param name="port">The parsed port component.</param>
    /// <param name="https">The parsed HTTPS flag.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    private static bool TryParseUrlSpec(string spec, out string host, out int port, out bool? https)
    {
        host = ""; port = 0; https = null;
        // Fast rejection for an explicitly empty port (e.g. "https://localhost:" or "http://[::1]:")
        // Uri.TryCreate will happily parse these and supply the default scheme port (80/443),
        // which would make us treat an intentionally empty port as a valid implicit port.
        // The accepted formats require either no colon at all (implicit default) OR a colon followed by digits.
        // Therefore pattern: scheme:// host-part : end-of-string (no digits after colon) should be rejected.
        if (EmptyPortDetectionRegex().IsMatch(spec))
        {
            return false;
        }
        if (!Uri.TryCreate(spec, UriKind.Absolute, out var uri))
        {
            return false;
        }
        if (!(uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)))
        {
            return false; // Not http/https → let other parsers try
        }
        if (uri.Authority.EndsWith(':'))
        {
            return false; // reject empty port like https://localhost:
        }
        host = uri.Host;
        port = uri.Port;
        https = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            ? true
            : uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                ? false
                : null;
        return !string.IsNullOrWhiteSpace(host) && IsValidPort(port);
    }

    /// <summary>
    /// Tries to parse a bracketed IPv6 endpoint specification.
    /// </summary>
    /// <param name="spec">The endpoint specification string.</param>
    /// <param name="host">The parsed host component.</param>
    /// <param name="port">The parsed port component.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    private static bool TryParseBracketedIpv6Spec(string spec, out string host, out int port)
    {
        host = ""; port = 0;
        var m = BracketedIpv6SpecMatcher().Match(spec);
        if (!m.Success)
        {
            return false;
        }
        host = m.Groups[1].Value;
        if (!int.TryParse(m.Groups[2].Value, out port) || !IsValidPort(port))
        {
            host = ""; port = 0; return false;
        }
        return !string.IsNullOrWhiteSpace(host);
    }

    /// <summary>
    /// Tries to parse a host:port endpoint specification.
    /// </summary>
    /// <param name="spec">The endpoint specification string.</param>
    /// <param name="host">The parsed host component.</param>
    /// <param name="port">The parsed port component.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    private static bool TryParseHostPortSpec(string spec, out string host, out int port)
    {
        host = ""; port = 0;
        var m = HostPortSpecMatcher().Match(spec);
        if (!m.Success)
        {
            return false;
        }
        host = m.Groups[1].Value;
        if (!int.TryParse(m.Groups[2].Value, out port) || !IsValidPort(port))
        {
            host = ""; port = 0; return false;
        }
        return !string.IsNullOrWhiteSpace(host);
    }
    private const int MIN_PORT = 1;
    private const int MAX_PORT = 65535;

    /// <summary>
    /// Validates that the port number is within the acceptable range (1-65535).
    /// </summary>
    /// <param name="port">The port number to validate.</param>
    /// <returns><c>true</c> if the port number is valid; otherwise, <c>false</c>.</returns>
    private static bool IsValidPort(int port) => port is >= MIN_PORT and <= MAX_PORT;

    /// <summary>
    /// Formats the host and port for use in RequireHost, adding brackets for IPv6 literals.
    /// </summary>
    /// <param name="host">The host component.</param>
    /// <param name="port">The port component.</param>
    /// <returns>The formatted host and port string.</returns>
    internal static string ToRequireHost(string host, int port) =>
        IsIPv6Address(host) ? $"[{host}]:{port}" : $"{host}:{port}"; // IPv6 literals must be bracketed in RequireHost

    /// <summary>
    /// Determines if the given host string is an IPv6 address.
    /// </summary>
    /// <param name="host">The host string to check.</param>
    /// <returns>True if the host is an IPv6 address; otherwise, false.</returns>
    private static bool IsIPv6Address(string host) => IPAddress.TryParse(host, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;

    /// <summary>
    /// Applies required hosts to the route based on the specified endpoints in the options.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    /// <exception cref="ArgumentException">Thrown when the specified endpoints are invalid.</exception>
    internal static void ApplyRequiredHost(this KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (options.Endpoints is not { Length: > 0 })
        {
            return;
        }

        var listeners = host.Options.Listeners;
        var require = new List<string>();
        var errs = new List<string>();

        foreach (var spec in options.Endpoints)
        {
            if (!TryParseEndpointSpec(spec, out var eh, out var ep, out var eHttps))
            {
                errs.Add($"'{spec}' must be 'host:port' or 'http(s)://host:port'.");
                continue;
            }

            // Is the host a numeric IP?
            var isNumericHost = IPAddress.TryParse(eh, out var endpointIp);

            // Find a compatible listener: same port, scheme (if specified), and IP match if numeric host.
            var match = listeners.FirstOrDefault(l =>
                l.Port == ep &&
                (eHttps is null || l.UseHttps == eHttps.Value) &&
                (!isNumericHost ||
                 l.IPAddress.Equals(endpointIp) ||
                 l.IPAddress.Equals(IPAddress.Any) ||
                 l.IPAddress.Equals(IPAddress.IPv6Any)));

            if (match is null)
            {
                errs.Add($"'{spec}' doesn't match any configured listener. " +
                         $"Known: {string.Join(", ", listeners.Select(l => l.ToString()))}");
                continue;
            }

            require.Add(ToRequireHost(eh, ep));
        }

        if (errs.Count > 0)
        {
            throw new InvalidOperationException("Invalid Endpoints:" + Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", errs));
        }
        if (require.Count > 0)
        {
            host.Logger.Verbose("Applying required hosts: {RequiredHosts} to route: {Pattern}",
                string.Join(", ", require), options.Pattern);
            _ = map.RequireHost([.. require]);
        }
    }

    /// <summary>
    /// Applies the same route conventions used by the AddMapRoute helpers to an arbitrary endpoint.
    /// </summary>
    /// <param name="host">The Kestrun host used for validation (auth schemes/policies).</param>
    /// <param name="builder">The endpoint convention builder to decorate.</param>
    /// <param name="configure">Delegate to configure a fresh <see cref="MapRouteOptions"/> instance. Only applicable properties are honored.</param>
    /// <remarks>
    /// This is useful when you map endpoints manually via <c>app.MapGet</c>/<c>MapPost</c> and still want consistent behavior
    /// (auth, CORS, rate limiting, antiforgery disable, OpenAPI metadata, short-circuiting) without re-implementing logic.
    /// Validation notes:
    ///  - Pattern, Code are ignored if not relevant.
    ///  - Authentication schemes and policies are validated against the host registry.
    ///  - OpenAPI metadata is applied only when non-empty.
    /// </remarks>
    /// <returns>The original <paramref name="builder"/> for fluent chaining.</returns>
    public static IEndpointConventionBuilder ApplyKestrunConventions(this KestrunHost host, IEndpointConventionBuilder builder, Action<MapRouteOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Start with an empty options record (only convention-related fields will matter)
        var options = new MapRouteOptions
        {
            Pattern = string.Empty,
            HttpVerbs = [],
            ScriptCode = new LanguageOptions
            {
                Language = ScriptLanguage.Native,
                Code = string.Empty
            }
        };
        configure(options);

        // Reuse internal helper (kept internal to avoid accidental misuse) for actual application
        host.AddMapOptions(builder, options);
        return builder;
    }

    /// <summary>
    /// Applies short-circuiting behavior to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void ApplyShortCircuit(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (!options.ShortCircuit)
        {
            return;
        }

        host.Logger.Verbose("Short-circuiting route: {Pattern} with status code: {StatusCode}", options.Pattern, options.ShortCircuitStatusCode);
        if (options.ShortCircuitStatusCode is null)
        {
            throw new ArgumentException("ShortCircuitStatusCode must be set if ShortCircuit is true.", nameof(options.ShortCircuitStatusCode));
        }

        _ = map.ShortCircuit(options.ShortCircuitStatusCode);
    }

    /// <summary>
    /// Applies anonymous access behavior to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void ApplyAnonymous(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (options.AllowAnonymous)
        {
            host.Logger.Verbose("Allowing anonymous access for route: {Pattern}", options.Pattern);
            _ = map.AllowAnonymous();
        }
        else
        {
            host.Logger.Debug("No anonymous access allowed for route: {Pattern}", options.Pattern);
        }
    }

    /// <summary>
    /// Disables anti-forgery behavior to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void DisableAntiforgery(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (!options.DisableAntiforgery)
        {
            return;
        }

        _ = map.DisableAntiforgery();
        host.Logger.Verbose("CSRF protection disabled for route: {Pattern}", options.Pattern);
    }

    /// <summary>
    /// Disables response compression for the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void DisableResponseCompression(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (!options.DisableResponseCompression)
        {
            return;
        }

        _ = map.DisableResponseCompression();
        host.Logger.Verbose("Response compression disabled for route: {Pattern}", options.Pattern);
    }
    /// <summary>
    /// Applies rate limiting behavior to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void ApplyRateLimiting(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RateLimitPolicyName))
        {
            return;
        }

        host.Logger.Verbose("Applying rate limit policy: {RateLimitPolicyName} to route: {Pattern}", options.RateLimitPolicyName, options.Pattern);
        _ = map.RequireRateLimiting(options.RateLimitPolicyName);
    }

    /// <summary>
    /// Applies authentication schemes to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void ApplyAuthSchemes(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (options.RequireSchemes is { Length: > 0 })
        {
            foreach (var schema in options.RequireSchemes)
            {
                if (!host.HasAuthScheme(schema))
                {
                    throw new ArgumentException($"Authentication scheme '{schema}' is not registered.", nameof(options.RequireSchemes));
                }
            }
            host.Logger.Verbose("Requiring authorization for route: {Pattern} with policies: {Policies}", options.Pattern, string.Join(", ", options.RequireSchemes));
            _ = map.RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = string.Join(',', options.RequireSchemes)
            });
        }
        else
        {
            host.Logger.Debug("No authorization required for route: {Pattern}", options.Pattern);
        }
    }

    /// <summary>
    /// Applies authorization policies to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void ApplyPolicies(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (options.RequirePolicies is { Length: > 0 })
        {
            foreach (var policy in options.RequirePolicies)
            {
                if (!host.HasAuthPolicy(policy))
                {
                    throw new ArgumentException($"Authorization policy '{policy}' is not registered.", nameof(options.RequirePolicies));
                }
            }
            _ = map.RequireAuthorization(options.RequirePolicies);
        }
        else
        {
            host.Logger.Debug("No authorization policies required for route: {Pattern}", options.Pattern);
        }
    }
    /// <summary>
    /// Applies CORS behavior to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void ApplyCors(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CorsPolicyName))
        {
            host.Logger.Verbose("Applying CORS policy: {CorsPolicyName} to route: {Pattern}", options.CorsPolicyName, options.Pattern);
            _ = map.RequireCors(options.CorsPolicyName);
        }
        else
        {
            host.Logger.Debug("No CORS policy applied for route: {Pattern}", options.Pattern);
        }
    }

    /// <summary>
    /// Applies OpenAPI metadata to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void ApplyOpenApiMetadata(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (!string.IsNullOrEmpty(options.OpenAPI.OperationId))
        {
            host.Logger.Verbose("Adding OpenAPI metadata for route: {Pattern} with OperationId: {OperationId}", options.Pattern, options.OpenAPI.OperationId);
            _ = map.WithName(options.OpenAPI.OperationId);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Summary))
        {
            host.Logger.Verbose("Adding OpenAPI summary for route: {Pattern} with Summary: {Summary}", options.Pattern, options.OpenAPI.Summary);
            _ = map.WithSummary(options.OpenAPI.Summary);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Description))
        {
            host.Logger.Verbose("Adding OpenAPI description for route: {Pattern} with Description: {Description}", options.Pattern, options.OpenAPI.Description);
            _ = map.WithDescription(options.OpenAPI.Description);
        }

        if (options.OpenAPI.Tags.Length > 0)
        {
            host.Logger.Verbose("Adding OpenAPI tags for route: {Pattern} with Tags: {Tags}", options.Pattern, string.Join(", ", options.OpenAPI.Tags));
            _ = map.WithTags(options.OpenAPI.Tags);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.GroupName))
        {
            host.Logger.Verbose("Adding OpenAPI group name for route: {Pattern} with GroupName: {GroupName}", options.Pattern, options.OpenAPI.GroupName);
            _ = map.WithGroupName(options.OpenAPI.GroupName);
        }
    }

    /// <summary>
    /// Adds an HTML template route to the KestrunHost for the specified pattern and HTML file path.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="htmlFilePath">The path to the HTML template file.</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddHtmlTemplateRoute(this KestrunHost host, string pattern, string htmlFilePath, string[]? requireSchemes = null)
    {
        return host.AddHtmlTemplateRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [HttpVerb.Get],
            RequireSchemes = requireSchemes ?? [] // No authorization by default
        }, htmlFilePath);
    }

    /// <summary>
    /// Adds an HTML template route to the KestrunHost using the specified MapRouteOptions and HTML file path.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <param name="htmlFilePath">The path to the HTML template file.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddHtmlTemplateRoute(this KestrunHost host, MapRouteOptions options, string htmlFilePath)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding HTML template route: {Pattern}", options.Pattern);
        }

        if (options.HttpVerbs.Count != 0 &&
            (options.HttpVerbs.Count > 1 || options.HttpVerbs.First() != HttpVerb.Get))
        {
            host.Logger.Error("HTML template routes only support GET requests. Provided HTTP verbs: {HttpVerbs}", string.Join(", ", options.HttpVerbs));
            throw new ArgumentException("HTML template routes only support GET requests.", nameof(options.HttpVerbs));
        }
        if (string.IsNullOrWhiteSpace(htmlFilePath) || !File.Exists(htmlFilePath))
        {
            host.Logger.Error("HTML file path is null, empty, or does not exist: {HtmlFilePath}", htmlFilePath);
            throw new FileNotFoundException("HTML file not found.", htmlFilePath);
        }

        if (string.IsNullOrWhiteSpace(options.Pattern))
        {
            host.Logger.Error("Pattern cannot be null or empty.");
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));
        }

        _ = host.AddMapRoute(options.Pattern, HttpVerb.Get, async (ctx) =>
          {
              // ② Build your variables map
              var vars = new Dictionary<string, object?>();
              _ = VariablesMap.GetVariablesMap(ctx, ref vars);

              await ctx.Response.WriteHtmlResponseFromFileAsync(htmlFilePath, vars, ctx.Response.StatusCode);
          }, out var map);
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Mapped HTML template route: {Pattern} to file: {HtmlFilePath}", options.Pattern, htmlFilePath);
        }
        if (map is null)
        {
            throw new InvalidOperationException("Failed to create HTML template route.");
        }
        AddMapOptions(host, map, options);
        return map;
    }

    /// <summary>
    /// Checks if a route with the specified pattern and optional HTTP method exists in the KestrunHost.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern to check.</param>
    /// <param name="verbs">The optional HTTP method to check for the route.</param>
    /// <returns>True if the route exists; otherwise, false.</returns>
    public static bool MapExists(this KestrunHost host, string pattern, IEnumerable<HttpVerb> verbs)
    {
        var methodSet = verbs.Select(v => v.ToMethodString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return host._registeredRoutes.Keys
            .Where(k => string.Equals(k.Pattern, pattern, StringComparison.OrdinalIgnoreCase))
            .Any(k => methodSet.Contains(k.Method));
    }

    /// <summary>
    /// Checks if a route with the specified pattern and optional HTTP method exists in the KestrunHost.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern to check.</param>
    /// <param name="verb">The optional HTTP method to check for the route.</param>
    /// <returns>True if the route exists; otherwise, false.</returns>
    public static bool MapExists(this KestrunHost host, string pattern, HttpVerb verb) =>
        host._registeredRoutes.ContainsKey((pattern, verb.ToMethodString()));


    /// <summary>
    /// Retrieves the <see cref="MapRouteOptions"/> associated with a given route pattern and HTTP verb, if registered.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to search for registered routes.</param>
    /// <param name="pattern">The route pattern to look up (e.g. <c>"/hello"</c>).</param>
    /// <param name="verb">The HTTP verb to match (e.g. <see cref="HttpVerb.Get"/>).</param>
    /// <returns>
    /// The <see cref="MapRouteOptions"/> instance for the specified route if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method checks the internal route registry and returns the route options if the pattern and verb
    /// combination was previously added via <c>AddMapRoute</c>.
    /// This lookup is case-insensitive for both the pattern and method.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = host.GetMapRouteOptions("/hello", HttpVerb.Get);
    /// if (options != null)
    /// {
    ///     Console.WriteLine($"Route language: {options.Language}");
    /// }
    /// </code>
    /// </example>
    public static MapRouteOptions? GetMapRouteOptions(this KestrunHost host, string pattern, HttpVerb verb)
    {
        return host._registeredRoutes.TryGetValue((pattern, verb.ToMethodString()), out var options)
            ? options
            : null;
    }

    /// <summary>
    /// Adds a GET endpoint that issues the antiforgery cookie and returns a JSON payload:
    /// { token: "...", headerName: "X-CSRF-TOKEN" }.
    /// The endpoint itself is exempt from antiforgery validation.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route path to expose (default "/csrf-token").</param>
    /// <returns>IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddAntiforgeryTokenRoute(
    this KestrunHost host,
    string pattern = "/csrf-token")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        if (host.App is null)
        {
            throw new InvalidOperationException("WebApplication is not initialized. Call EnableConfiguration first.");
        }
        var options = new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Language = ScriptLanguage.Native
            },
            DisableAntiforgery = true,
            AllowAnonymous = true,
        };
        // OpenAPI = new() { Summary = "Get CSRF token", Description = "Returns antiforgery request token and header name." }

        // Map directly and write directly (no KestrunResponse.ApplyTo)
        var map = host.App.MapMethods(options.Pattern, [HttpMethods.Get], async context =>
        {
            var af = context.RequestServices.GetRequiredService<IAntiforgery>();
            var opts = context.RequestServices.GetRequiredService<IOptions<AntiforgeryOptions>>();

            var tokens = af.GetAndStoreTokens(context);

            // Strongly discourage caches (proxies/browsers) from storing this payload
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                token = tokens.RequestToken,
                headerName = opts.Value.HeaderName // may be null if not configured
            });
        });

        // Apply your pipeline metadata (this adds DisableAntiforgery, CORS, rate limiting, OpenAPI, etc.)
        host.AddMapOptions(map, options);

        // (Optional) track in your registry for consistency / duplicate checks
        host._registeredRoutes[(options.Pattern, HttpMethods.Get)] = options;

        host.Logger.Information("Added token endpoint: {Pattern} (GET)", options.Pattern);
        return map;
    }

    private static bool IsUnsafeVerb(HttpVerb v)
        => v is HttpVerb.Post or HttpVerb.Put or HttpVerb.Patch or HttpVerb.Delete;

    private static bool IsUnsafeMethod(string method)
        => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    // New precise helper: only validate for the actual incoming request method when that method is unsafe and antiforgery is enabled.
    private static bool ShouldValidateCsrf(MapRouteOptions o, HttpContext ctx)
    {
        if (o.DisableAntiforgery)
        {
            return false;
        }
        if (!IsUnsafeMethod(ctx.Request.Method))
        {
            return false; // Safe verb (GET/HEAD/OPTIONS) -> skip
        }
        // Ensure the route was actually configured for this unsafe verb (defensive; normally true inside mapped delegate)
        return o.HttpVerbs.Any(v => string.Equals(v.ToMethodString(), ctx.Request.Method, StringComparison.OrdinalIgnoreCase) && IsUnsafeVerb(v));
    }

    private static async Task<bool> TryValidateAntiforgeryAsync(HttpContext ctx)
    {
        var af = ctx.RequestServices.GetService<IAntiforgery>();
        if (af is null)
        {
            return true; // antiforgery not configured → do nothing
        }

        try
        {
            await af.ValidateRequestAsync(ctx);
            return true;
        }
        catch (AntiforgeryValidationException ex)
        {
            // short-circuit with RFC 9110 problem+json
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                title = "Antiforgery validation failed",
                status = 400,
                detail = ex.Message
            });
            return false;
        }
    }

    /// <summary>
    /// Matches a bracketed IPv6 host:port specification in the format "[ipv6]:port", where:
    /// - ipv6 is a valid IPv6 address (e.g. "::1", "2001:0db8:85a3:0000:0000:8a2e:0370:7334")
    /// - port is a numeric value between 1 and 65535
    /// Examples of valid inputs:
    ///   "[::1]:80"
    ///   "[2001:0db8:85a3:0000:0000:8a2e:0370:7334]:443"
    /// </summary>
    [GeneratedRegex(@"^\[([^\]]+)\]:(\d+)$")]
    private static partial Regex BracketedIpv6SpecMatcher();

    /// <summary>
    /// Matches a host:port specification in the format "host:port", where:
    /// - host can be any string excluding ':' (to avoid confusion with IPv6 addresses)
    /// - port is a numeric value between 1 and 65535
    /// Examples of valid inputs:
    ///   "example.com:80"
    ///   "localhost:443"
    ///   "[::1]:8080"  (IPv6 address in brackets)
    /// </summary>
    [GeneratedRegex(@"^([^:]+):(\d+)$")]
    private static partial Regex HostPortSpecMatcher();


    /// <summary>
    /// Matches a URL that starts with "http://" or "https://", followed by a host (excluding '/', '?', or '#'), and ends with a colon.
    /// Examples of valid inputs:
    ///   "http://example.com:"
    ///   "https://localhost:"
    ///   "https://my-server:8080:"
    /// </summary>
    [GeneratedRegex(@"^https?://[^/\?#]+:$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex EmptyPortDetectionRegex();
}
