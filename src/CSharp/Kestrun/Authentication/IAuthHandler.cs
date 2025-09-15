


using System.Collections;
using System.Management.Automation;
using System.Security.Claims;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.SharedState;
using Microsoft.AspNetCore.Authentication;

namespace Kestrun.Authentication;


/// <summary>
/// Defines common options for authentication, including code validation, claim issuance, and claim policy configuration.
/// </summary>
public interface IAuthHandler
{
    /// <summary>
    /// Generates an <see cref="AuthenticationTicket"/> for the specified user and authentication scheme, issuing additional claims as configured.
    /// </summary>
    /// <param name="Context">The current HTTP context.</param>
    /// <param name="user">The user name for whom the ticket is being generated.</param>
    /// <param name="Options">Authentication options including claim issuance delegates.</param>
    /// <param name="Scheme">The authentication scheme to use.</param>
    /// <param name="alias">An optional alias for the user.</param>
    /// <returns>An <see cref="AuthenticationTicket"/> representing the authenticated user.</returns>
    static async Task<AuthenticationTicket> GetAuthenticationTicketAsync(
        HttpContext Context, string user,
    IAuthenticationCommonOptions Options, AuthenticationScheme Scheme, string? alias = null)
    {
        var claims = new List<Claim>();

        // 1) Issue extra claims if configured
        claims.AddRange(await GetIssuedClaimsAsync(Context, user, Options).ConfigureAwait(false));

        // 2) Ensure a Name claim exists
        EnsureNameClaim(claims, user, alias, Options.Logger);

        // 3) Create and return the ticket
        return CreateAuthenticationTicket(claims, Scheme);
    }

    /// <summary>
    /// Issues claims for the specified user based on the provided context and options.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="user">The user name for whom claims are being issued.</param>
    /// <param name="options">Authentication options including claim issuance delegates.</param>
    /// <returns>A collection of issued claims.</returns>
    private static async Task<IEnumerable<Claim>> GetIssuedClaimsAsync(HttpContext context, string user, IAuthenticationCommonOptions options)
    {
        if (options.IssueClaims is null)
        {
            return [];
        }

        var extra = await options.IssueClaims(context, user).ConfigureAwait(false);
        if (extra is null)
        {
            return [];
        }

        // Filter out nulls and empty values
        return [.. extra
            .Where(c => c is not null)
            .OfType<Claim>()
            .Where(c => !string.IsNullOrEmpty(c.Value))];
    }

    /// <summary>
    /// Ensures that a Name claim is present in the list of claims.
    /// </summary>
    /// <param name="claims">The list of claims to check.</param>
    /// <param name="user">The user name to use if a Name claim is added.</param>
    /// <param name="alias">An optional alias for the user.</param>
    /// <param name="logger">The logger instance.</param>
    private static void EnsureNameClaim(List<Claim> claims, string user, string? alias, Serilog.ILogger logger)
    {
        if (claims.Any(c => c.Type == ClaimTypes.Name))
        {
            return;
        }

        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("No Name claim found, adding default Name claim");
        }

        var name = string.IsNullOrEmpty(alias) ? user : alias;
        claims.Add(new Claim(ClaimTypes.Name, name!));
    }

    /// <summary>
    /// Creates an authentication ticket from the specified claims and authentication scheme.
    /// </summary>
    /// <param name="claims">The claims to include in the ticket.</param>
    /// <param name="scheme">The authentication scheme to use.</param>
    /// <returns>An authentication ticket containing the specified claims.</returns>
    private static AuthenticationTicket CreateAuthenticationTicket(IEnumerable<Claim> claims, AuthenticationScheme scheme)
    {
        var claimsIdentity = new ClaimsIdentity(claims, scheme.Name);
        var principal = new ClaimsPrincipal(claimsIdentity);
        return new AuthenticationTicket(principal, scheme.Name);
    }

    /// <summary>
    /// Authenticates the user using PowerShell script.
    /// This method is used to validate the username and password against a PowerShell script.
    /// </summary>
    /// <param name="code">The PowerShell script code used for authentication.</param>
    /// <param name="context">The HTTP context.</param>
    /// <param name="credentials">A dictionary containing the credentials to validate (e.g., username and password).</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    static async ValueTask<bool> ValidatePowerShellAsync(string? code, HttpContext context, Dictionary<string, string> credentials, Serilog.ILogger logger)
    {
        try
        {
            if (!context.Items.ContainsKey("PS_INSTANCE"))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }
            // Validate that the credentials dictionary is not null or empty
            if (credentials == null || credentials.Count == 0)
            {
                logger.Warning("Credentials are null or empty.");
                return false;
            }
            // Validate that the code is not null or empty
            if (string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException("PowerShell authentication code is null or empty.");
            }
            // Retrieve the PowerShell instance from the context
            var ps = context.Items["PS_INSTANCE"] as PowerShell
                  ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
            if (ps.Runspace == null)
            {
                throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.");
            }

            _ = ps.AddScript(code, useLocalScope: true);
            foreach (var kvp in credentials)
            {
                _ = ps.AddParameter(kvp.Key, kvp.Value);
            }

            var psResults = await ps.InvokeAsync().ConfigureAwait(false);

            if (psResults.Count == 0 || psResults[0] == null || psResults[0].BaseObject is not bool isValid)
            {
                logger.Error("PowerShell script did not return a valid boolean result.");
                return false;
            }
            return isValid;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during validating PowerShell authentication.");
            return false;
        }
    }

    /// <summary>
    /// Builds a C# validator function for the specified authentication settings.
    /// </summary>
    /// <param name="settings">The authentication code settings.</param>
    /// <param name="log">The logger instance.</param>
    /// <param name="globals">Global variables to include in the validation context.</param>
    /// <returns>A function that validates the authentication context.</returns>
    internal static Func<HttpContext, IDictionary<string, object?>, Task<bool>> BuildCsValidator(
        AuthenticationCodeSettings settings,
        Serilog.ILogger log,
          params (string Name, object? Prototype)[] globals)
    {
        if (log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            log.Debug("Building C# authentication script with globals: {Globals}", globals);
        }

        // Place-holders so Roslyn knows the globals that will exist
        var stencil = globals.ToDictionary(n => n.Name, n => n.Prototype,
                                             StringComparer.OrdinalIgnoreCase);
        if (log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            log.Debug("Compiling C# authentication script with variables: {Variables}", stencil);
        }

        var script = CSharpDelegateBuilder.Compile(
            settings.Code,
            log,                             // already scoped by caller
            settings.ExtraImports,
            settings.ExtraRefs,
            stencil,
            languageVersion: settings.CSharpVersion);

        // Return the runtime delegate
        return async (ctx, vars) =>
        {
            if (log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                log.Debug("Running C# authentication script with variables: {Variables}", vars);
            }
            // --- Kestrun plumbing -------------------------------------------------
            var krReq = await KestrunRequest.NewRequest(ctx);
            var krRes = new KestrunResponse(krReq);
            var kCtx = new KestrunContext(krReq, krRes, ctx);
            // ---------------------------------------------------------------------
            var globalsDict = new Dictionary<string, object?>(
                    vars, StringComparer.OrdinalIgnoreCase);
            // Merge shared state + user variables
            var globals = new CsGlobals(
                SharedStateStore.Snapshot(),
                kCtx,
                globalsDict);

            var result = await script.RunAsync(globals).ConfigureAwait(false);
            return result.ReturnValue is true;
        };
    }

    internal static Func<HttpContext, IDictionary<string, object?>, Task<bool>> BuildVBNetValidator(
        AuthenticationCodeSettings settings,
        Serilog.ILogger log,
      params (string Name, object? Prototype)[] globals)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings), "AuthenticationCodeSettings cannot be null");
        }
        // Place-holders so Roslyn knows the globals that will exist
        var stencil = globals.ToDictionary(n => n.Name, n => n.Prototype,
                                                 StringComparer.OrdinalIgnoreCase);

        if (log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            log.Debug("Compiling VB.NET authentication script with variables: {Variables}", stencil);
        }

        // Compile the VB.NET script with the provided settings
        var script = VBNetDelegateBuilder.Compile<bool>(
            settings.Code,
            log,                             // already scoped by caller
            settings.ExtraImports,
            settings.ExtraRefs,
            stencil,
            languageVersion: settings.VisualBasicVersion);

        // Return the runtime delegate
        return async (ctx, vars) =>
        {
            if (log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                log.Debug("Running VB.NET authentication script with variables: {Variables}", vars);
            }

            // --- Kestrun plumbing -------------------------------------------------
            var krReq = await KestrunRequest.NewRequest(ctx);
            var krRes = new KestrunResponse(krReq);
            var kCtx = new KestrunContext(krReq, krRes, ctx);
            // ---------------------------------------------------------------------

            // Merge shared state + user variables
            var globals = new CsGlobals(
                SharedStateStore.Snapshot(),
                kCtx,
                new Dictionary<string, object?>(vars, StringComparer.OrdinalIgnoreCase));

            var result = await script(globals).ConfigureAwait(false);

            return result is bool isValid && isValid;
        };
    }


    /// <summary>
    /// Builds a PowerShell-based function for issuing claims for a user.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the PowerShell script.</param>
    /// <param name="logger">The logger instance for logging.</param>
    /// <returns>A function that issues claims using the provided PowerShell script.</returns>
    static Func<HttpContext, string, Task<IEnumerable<Claim>>> BuildPsIssueClaims(
        AuthenticationCodeSettings settings, Serilog.ILogger logger) =>
            async (ctx, identity) =>
        {
            return await IssueClaimsPowerShellAsync(settings.Code, ctx, identity, logger);
        };

    /// <summary>
    /// Issues claims for a user by executing a PowerShell script.
    /// </summary>
    /// <param name="code">The PowerShell script code used to issue claims.</param>
    /// <param name="ctx">The HTTP context containing the PowerShell runspace.</param>
    /// <param name="identity">The username for which to issue claims.</param>
    /// <param name="logger">The logger instance for logging.</param>
    /// <returns>A task representing the asynchronous operation, with a collection of issued claims.</returns>
    static async Task<IEnumerable<Claim>> IssueClaimsPowerShellAsync(string? code, HttpContext ctx, string identity, Serilog.ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            logger.Warning("Identity is null or empty.");
            return [];
        }
        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("PowerShell authentication code is null or empty.");
        }

        try
        {
            var ps = GetPowerShell(ctx);
            _ = ps.AddScript(code, useLocalScope: true).AddParameter("identity", identity);

            var psResults = await ps.InvokeAsync().ConfigureAwait(false);
            if (psResults is null || psResults.Count == 0)
            {
                return [];
            }

            var claims = new List<Claim>(psResults.Count);
            foreach (var r in psResults)
            {
                if (TryToClaim(r?.BaseObject, out var claim))
                {
                    claims.Add(claim);
                }
                else
                {
                    logger.Warning("PowerShell script returned an unsupported type: {Type}", r?.BaseObject?.GetType());
                    throw new InvalidOperationException("PowerShell script returned an unsupported type.");
                }
            }

            return claims;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during Issue Claims for {Identity}", identity);
            return [];
        }
    }

    /// <summary>
    /// Retrieves the PowerShell instance from the HTTP context.
    /// </summary>
    /// <param name="ctx">The HTTP context containing the PowerShell runspace.</param>
    /// <returns>The PowerShell instance associated with the context.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the PowerShell runspace is not found.</exception>
    private static PowerShell GetPowerShell(HttpContext ctx)
    {
        return !ctx.Items.TryGetValue("PS_INSTANCE", out var psObj) || psObj is not PowerShell ps || ps.Runspace == null
            ? throw new InvalidOperationException("PowerShell runspace not found or not set in context items. Ensure PowerShellRunspaceMiddleware is registered.")
            : ps;
    }

    /// <summary>
    /// Tries to create a Claim from the provided object.
    /// </summary>
    /// <param name="obj">The object to create a Claim from.</param>
    /// <param name="claim">The created Claim, if successful.</param>
    /// <returns>True if the Claim was created successfully; otherwise, false.</returns>
    private static bool TryToClaim(object? obj, out Claim claim)
    {
        switch (obj)
        {
            case Claim c:
                claim = c;
                return true;

            case IDictionary dict when dict.Contains("Type") && dict.Contains("Value"):
                var typeStr = dict["Type"]?.ToString();
                var valueStr = dict["Value"]?.ToString();
                if (!string.IsNullOrEmpty(typeStr) && !string.IsNullOrEmpty(valueStr))
                {
                    claim = new Claim(typeStr, valueStr);
                    return true;
                }
                break;

            case string s when s.Contains(':'):
                var idx = s.IndexOf(':');
                if (idx >= 0 && idx < s.Length - 1)
                {
                    claim = new Claim(s[..idx], s[(idx + 1)..]);
                    return true;
                }
                break;
            default:
                // Unsupported type
                break;
        }

        claim = default!;
        return false;
    }


    /// <summary>
    /// Builds a C#-based function for issuing claims for a user.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the C# script.</param>
    /// <param name="logger">The logger instance for logging.</param>
    /// <returns>A function that issues claims using the provided C# script.</returns>
    static Func<HttpContext, string, Task<IEnumerable<Claim>>> BuildCsIssueClaims(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Compiling C# script for issuing claims.");
        }

        // Compile the C# script with the provided settings
        var script = CSharpDelegateBuilder.Compile(settings.Code, logger,
        settings.ExtraImports, settings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "identity", "" }
            }, languageVersion: settings.CSharpVersion);

        return async (ctx, identity) =>
        {
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var globals = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "identity", identity }
            });
            var result = await script.RunAsync(globals).ConfigureAwait(false);
            return result.ReturnValue is IEnumerable<Claim> claims
                ? claims
                : [];
        };
    }


    /// <summary>
    /// Builds a VB.NET-based function for issuing claims for a user.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the VB.NET script.</param>
    /// <param name="logger">The logger instance for logging.</param>
    /// <returns>A function that issues claims using the provided VB.NET script.</returns>
    static Func<HttpContext, string, Task<IEnumerable<Claim>>> BuildVBNetIssueClaims(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Compiling VB.NET script for issuing claims.");
        }

        // Compile the VB.NET script with the provided settings
        var script = VBNetDelegateBuilder.Compile<IEnumerable<Claim>>(settings.Code, logger,
        settings.ExtraImports, settings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "identity", "" }
            }, languageVersion: settings.VisualBasicVersion);

        return async (ctx, identity) =>
        {
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var glob = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "identity", identity }
            });
            // Run the VB.NET script and get the result
            // Note: The script should return a boolean indicating success or failure
            var result = await script(glob).ConfigureAwait(false);
            return result is IEnumerable<Claim> claims
              ? claims
           : [];
        };
    }
}
