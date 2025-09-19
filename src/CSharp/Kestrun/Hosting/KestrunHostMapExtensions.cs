
using Kestrun.Hosting.Options;
using Kestrun.Languages;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Authorization;
using Serilog.Events;
using Kestrun.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for mapping routes and handlers to the KestrunHost.
/// </summary>
public static class KestrunHostMapExtensions
{
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
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern, HttpVerb httpVerb, KestrunHandler handler, string[]? requireSchemes = null) => host.AddMapRoute(pattern: pattern, httpVerbs: [httpVerb], handler: handler, requireSchemes: requireSchemes);

    /// <summary>
    /// Adds a native route to the KestrunHost for the specified pattern and HTTP verbs.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpVerbs">The HTTP verbs for the route.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern, IEnumerable<HttpVerb> httpVerbs, KestrunHandler handler, string[]? requireSchemes = null)
    {
        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [.. httpVerbs],
            Language = ScriptLanguage.Native,
            RequireSchemes = requireSchemes ?? [] // No authorization by default
        }, handler);
    }

    /// <summary>
    /// Adds a native route to the KestrunHost using the specified MapRouteOptions and handler.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, MapRouteOptions options, KestrunHandler handler)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("AddMapRoute called with options={Options}", options);
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
        var map = host.App.MapMethods(options.Pattern, methods, async context =>
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
               KestrunContext kestrunContext = new(req, res, context);
               await handler(kestrunContext);
               await res.ApplyTo(context.Response);
           });

        host.AddMapOptions(map, options);

        host.HostLogger.Information("Added native route: {Pattern} with methods: {Methods}", options.Pattern, string.Join(", ", methods));
        // Add to the feature queue for later processing
        host.FeatureQueue.Add(host => host.AddMapRoute(options));
        return map;
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
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern, HttpVerb httpVerbs, string scriptBlock, ScriptLanguage language = ScriptLanguage.PowerShell,
                                     string[]? requireSchemes = null,
                                 Dictionary<string, object?>? arguments = null)
    {
        arguments ??= [];
        return host.AddMapRoute(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [httpVerbs],
            Code = scriptBlock,
            Language = language,
            RequireSchemes = requireSchemes ?? [], // No authorization by default
            Arguments = arguments ?? [] // No additional arguments by default
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
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, string pattern,
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
            Code = scriptBlock,
            Language = language,
            RequireSchemes = requireSchemes ?? [], // No authorization by default
            Arguments = arguments ?? [] // No additional arguments by default
        });
    }

    /// <summary>
    /// Adds a route to the KestrunHost using the specified MapRouteOptions.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder AddMapRoute(this KestrunHost host, MapRouteOptions options)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("AddMapRoute called with pattern={Pattern}, language={Language}, method={Methods}", options.Pattern, options.Language, options.HttpVerbs);
        }

        try
        {
            // Validate options and get normalized route options
            if (!ValidateRouteOptions(host, options, out var routeOptions))
            {
                return null!; // Route already exists and should be skipped
            }

            var logger = host.HostLogger.ForContext("Route", routeOptions.Pattern);

            // Compile the script once – return a RequestDelegate
            var compiled = CompileScript(routeOptions, logger);

            // Create and register the route
            return CreateAndRegisterRoute(host, routeOptions, compiled);
        }
        catch (CompilationErrorException ex)
        {
            // Log the detailed compilation errors
            host.HostLogger.Error($"Failed to add route '{options.Pattern}' due to compilation errors:");
            host.HostLogger.Error(ex.GetDetailedErrorMessage());

            // Re-throw with additional context
            throw new InvalidOperationException(
                $"Failed to compile {options.Language} script for route '{options.Pattern}'. {ex.GetErrors().Count()} error(s) found.",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to add route '{options.Pattern}' with method '{string.Join(", ", options.HttpVerbs)}' using {options.Language}: {ex.Message}",
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
        if (string.IsNullOrWhiteSpace(options.Code))
        {
            throw new ArgumentException("ScriptBlock cannot be null or empty.", nameof(options.Code));
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

            host.HostLogger.Warning(msg);
            return false; // Skip this route
        }

        return true; // Continue with route creation
    }

    /// <summary>
    /// Compiles the script code for the specified language.
    /// </summary>
    /// <param name="options">The MapRouteOptions containing the script and language.</param>
    /// <param name="logger">The Serilog logger to use for compilation.</param>
    /// <returns>A compiled RequestDelegate that can handle HTTP requests.</returns>
    /// <exception cref="NotSupportedException">Thrown when the script language is not supported.</exception>
    internal static RequestDelegate CompileScript(MapRouteOptions options, Serilog.ILogger logger)
    {
        return options.Language switch
        {
            ScriptLanguage.PowerShell => PowerShellDelegateBuilder.Build(options.Code!, logger, options.Arguments),
            ScriptLanguage.CSharp => CSharpDelegateBuilder.Build(options.Code!, logger, options.Arguments, options.ExtraImports, options.ExtraRefs),
            ScriptLanguage.VBNet => VBNetDelegateBuilder.Build(options.Code!, logger, options.Arguments, options.ExtraImports, options.ExtraRefs),
            ScriptLanguage.FSharp => FSharpDelegateBuilder.Build(options.Code!, logger), // F# scripting not implemented
            ScriptLanguage.Python => PyDelegateBuilder.Build(options.Code!, logger),
            ScriptLanguage.JavaScript => JScriptDelegateBuilder.Build(options.Code!, logger),
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
        var map = host.App!.MapMethods(routeOptions.Pattern!, methods, handler).WithLanguage(routeOptions.Language);

        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Mapped route: {Pattern} with methods: {Methods}", routeOptions.Pattern, string.Join(", ", methods));
        }

        host.AddMapOptions(map, routeOptions);

        foreach (var method in routeOptions.HttpVerbs.Select(v => v.ToMethodString()))
        {
            host._registeredRoutes[(routeOptions.Pattern!, method)] = routeOptions;
        }

        host.HostLogger.Information("Added route: {Pattern} with methods: {Methods}", routeOptions.Pattern, string.Join(", ", methods));
        return map;
    }

    /// <summary>
    /// Adds additional mapping options to the route.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="map">The endpoint convention builder.</param>
    /// <param name="options">The mapping options.</param>
    private static void AddMapOptions(this KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        ApplyShortCircuit(host, map, options);
        ApplyAnonymous(host, map, options);
        DisableAntiforgery(host, map, options);
        ApplyRateLimiting(host, map, options);
        ApplyAuthSchemes(host, map, options);
        ApplyPolicies(host, map, options);
        ApplyCors(host, map, options);
        ApplyOpenApiMetadata(host, map, options);
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

        host.HostLogger.Verbose("Short-circuiting route: {Pattern} with status code: {StatusCode}", options.Pattern, options.ShortCircuitStatusCode);
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
            host.HostLogger.Verbose("Allowing anonymous access for route: {Pattern}", options.Pattern);
            _ = map.AllowAnonymous();
        }
        else
        {
            host.HostLogger.Debug("No anonymous access allowed for route: {Pattern}", options.Pattern);
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
        host.HostLogger.Verbose("CSRF protection disabled for route: {Pattern}", options.Pattern);
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

        host.HostLogger.Verbose("Applying rate limit policy: {RateLimitPolicyName} to route: {Pattern}", options.RateLimitPolicyName, options.Pattern);
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
            host.HostLogger.Verbose("Requiring authorization for route: {Pattern} with policies: {Policies}", options.Pattern, string.Join(", ", options.RequireSchemes));
            _ = map.RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = string.Join(',', options.RequireSchemes)
            });
        }
        else
        {
            host.HostLogger.Debug("No authorization required for route: {Pattern}", options.Pattern);
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
            host.HostLogger.Debug("No authorization policies required for route: {Pattern}", options.Pattern);
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
            host.HostLogger.Verbose("Applying CORS policy: {CorsPolicyName} to route: {Pattern}", options.CorsPolicyName, options.Pattern);
            _ = map.RequireCors(options.CorsPolicyName);
        }
        else
        {
            host.HostLogger.Debug("No CORS policy applied for route: {Pattern}", options.Pattern);
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
            host.HostLogger.Verbose("Adding OpenAPI metadata for route: {Pattern} with OperationId: {OperationId}", options.Pattern, options.OpenAPI.OperationId);
            _ = map.WithName(options.OpenAPI.OperationId);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Summary))
        {
            host.HostLogger.Verbose("Adding OpenAPI summary for route: {Pattern} with Summary: {Summary}", options.Pattern, options.OpenAPI.Summary);
            _ = map.WithSummary(options.OpenAPI.Summary);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Description))
        {
            host.HostLogger.Verbose("Adding OpenAPI description for route: {Pattern} with Description: {Description}", options.Pattern, options.OpenAPI.Description);
            _ = map.WithDescription(options.OpenAPI.Description);
        }

        if (options.OpenAPI.Tags.Length > 0)
        {
            host.HostLogger.Verbose("Adding OpenAPI tags for route: {Pattern} with Tags: {Tags}", options.Pattern, string.Join(", ", options.OpenAPI.Tags));
            _ = map.WithTags(options.OpenAPI.Tags);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.GroupName))
        {
            host.HostLogger.Verbose("Adding OpenAPI group name for route: {Pattern} with GroupName: {GroupName}", options.Pattern, options.OpenAPI.GroupName);
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
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding HTML template route: {Pattern}", options.Pattern);
        }

        if (options.HttpVerbs.Count() != 0 &&
            (options.HttpVerbs.Count() > 1 || options.HttpVerbs.First() != HttpVerb.Get))
        {
            host.HostLogger.Error("HTML template routes only support GET requests. Provided HTTP verbs: {HttpVerbs}", string.Join(", ", options.HttpVerbs));
            throw new ArgumentException("HTML template routes only support GET requests.", nameof(options.HttpVerbs));
        }
        if (string.IsNullOrWhiteSpace(htmlFilePath) || !File.Exists(htmlFilePath))
        {
            host.HostLogger.Error("HTML file path is null, empty, or does not exist: {HtmlFilePath}", htmlFilePath);
            throw new FileNotFoundException("HTML file not found.", htmlFilePath);
        }

        if (string.IsNullOrWhiteSpace(options.Pattern))
        {
            host.HostLogger.Error("Pattern cannot be null or empty.");
            throw new ArgumentException("Pattern cannot be null or empty.", nameof(options.Pattern));
        }

        var map = host.AddMapRoute(options.Pattern, HttpVerb.Get, async (ctx) =>
          {
              // ② Build your variables map
              var vars = new Dictionary<string, object?>();
              _ = VariablesMap.GetVariablesMap(ctx, ref vars);

              await ctx.Response.WriteHtmlResponseFromFileAsync(htmlFilePath, vars, ctx.Response.StatusCode);
          });

        AddMapOptions(host, map, options);
        return map;
    }

    /// <summary>
    /// Adds a static override route to the KestrunHost for the specified pattern and handler.
    /// This allows you to override static file serving with dynamic content.
    /// Call this method before adding static file components to ensure it takes precedence.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern to match.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    /// <remarks>
    /// This method allows you to override static file serving with dynamic content by providing a handler
    /// that will be executed for the specified route pattern.
    /// </remarks>
    public static KestrunHost AddStaticMapOverride(
        this KestrunHost host,
        string pattern,
        KestrunHandler handler,
        string[]? requireSchemes = null)         // same delegate you already use
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return host.AddStaticMapOverride(new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [HttpVerb.Get], // GET-only
            Language = ScriptLanguage.Native,
            RequireSchemes = requireSchemes ?? [] // No authorization by default
        }, handler);
    }

    /// <summary>
    /// Adds a static override route to the KestrunHost using the specified MapRouteOptions and handler.
    /// This allows you to override static file serving with dynamic content by providing a handler
    /// that will be executed for the specified route pattern.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <param name="handler">The handler to execute for the route.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    public static KestrunHost AddStaticMapOverride(
        this KestrunHost host,
        MapRouteOptions options,
        KestrunHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Pattern);

        // Check if the host is already configured
        if (host.IsConfigured)
        {
            throw new InvalidOperationException("Kestrun host is already configured.");
        }

        // queue before static files – defer until WebApplication is being built
        return host.Use(app =>
        {
            var endpoints = (IEndpointRouteBuilder)app;        // ← key change

            // you said: static-file override should always be GET
            var map = endpoints.MapMethods(
                options.Pattern,
                [HttpMethods.Get],
                async ctx =>
                {
                    // wrap ASP.NET types in Kestrun abstractions
                    var req = await KestrunRequest.NewRequest(ctx);
                    var res = new KestrunResponse(req);
                    var kestrun = new KestrunContext(req, res, ctx);

                    await handler(kestrun);        // your logic
                    await res.ApplyTo(ctx.Response);
                });

            // apply ShortCircuit / CORS / auth etc.
            host.AddMapOptions(map, options);
        });
    }

    /// <summary>
    /// Adds a static override route to the KestrunHost for the specified pattern and script code.
    /// This allows you to override static file serving with dynamic content.
    /// /// Call this method before adding static file components to ensure it takes precedence.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="pattern">The route pattern to match.</param>
    /// <param name="code">The script code to execute.</param>
    /// <param name="language">The scripting language to use.</param>
    /// <param name="requireSchemes">Optional array of authorization schemes required for the route.</param>
    /// <param name="arguments">Optional dictionary of arguments to pass to the script.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    /// <remarks>
    /// This method allows you to override static file serving with dynamic content by providing a script
    /// that will be executed for the specified route pattern.
    /// </remarks>
    public static KestrunHost AddStaticMapOverride(
    this KestrunHost host,
         string pattern,
         string code,
         ScriptLanguage language = ScriptLanguage.PowerShell,
         string[]? requireSchemes = null,
         Dictionary<string, object?>? arguments = null
           )
    {
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
            ArgumentException.ThrowIfNullOrWhiteSpace(code);

            var options = new MapRouteOptions
            {
                Pattern = pattern,
                HttpVerbs = [HttpVerb.Get], // GET-only
                Code = code,
                Language = language,
                RequireSchemes = requireSchemes ?? [], // No authorization by default
                Arguments = arguments ?? [], // No additional arguments by default
            };
            // queue before static files
            return host.Use(app =>
            {
                _ = AddMapRoute(host, options);
            });
        }
    }


    /// <summary>
    /// Adds a static override route to the KestrunHost using the specified MapRouteOptions.
    /// This allows you to override static file serving with dynamic content.
    /// Call this method before adding static file components to ensure it takes precedence.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="options">The MapRouteOptions containing route configuration.</param>
    /// <returns>The KestrunHost instance for method chaining.</returns>
    public static KestrunHost AddStaticMapOverride(this KestrunHost host, MapRouteOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Code);
        // queue before static files
        return host.Use(app =>
        {
            _ = AddMapRoute(host, options);
        });
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
            Language = ScriptLanguage.Native,
            DisableAntiforgery = true,
            AllowAnonymous = true,                 // ← token endpoint should be public
                                                   // OpenAPI = new() { Summary = "Get CSRF token", Description = "Returns antiforgery request token and header name." }
        };

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

        host.HostLogger.Information("Added token endpoint: {Pattern} (GET)", options.Pattern);
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
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                title = "Antiforgery validation failed",
                status = 400,
                detail = ex.Message
            });
            return false;
        }
    }
}

