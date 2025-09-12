using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Kestrun.Authentication;

/// <summary>
/// Handles API Key authentication for incoming HTTP requests.
/// </summary>
public class ApiKeyAuthHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The options monitor for API key authentication options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        if (options.CurrentValue.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            options.CurrentValue.Logger.Debug("ApiKeyAuthHandler initialized");
        }
    }

    /// <summary>
    /// Authenticates the incoming request using an API key.
    /// </summary>
    /// <returns>An <see cref="AuthenticateResult"/> indicating the authentication outcome.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (Options.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                Options.Logger.Debug("Handling API Key authentication for request: {Request}", Request);
            }

            if (Options.RequireHttps && !Request.IsHttps)
            {
                return Fail("HTTPS required");
            }

            if (!TryGetApiKey(out var providedKey))
            {
                return Fail("Missing API Key");
            }

            var providedKeyBytes = Encoding.UTF8.GetBytes(providedKey);
            var valid = await ValidateApiKeyAsync(providedKey, providedKeyBytes);
            if (!valid)
            {
                return Fail($"Invalid API Key: {providedKey}");
            }

            var ticket = await CreateTicketAsync(providedKey);
            if (ticket.Principal is null)
            {
                return Fail("Authentication ticket has no principal");
            }
            Options.Logger.Information("API Key authentication succeeded for identity: {Identity}", ticket.Principal.Identity?.Name ?? "Unknown");

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Options.Logger.Error(ex, "API Key authentication failed with exception.");
            return Fail("Error processing API Key");
        }
    }

    /// <summary>
    /// Tries to retrieve the API key from the request headers or query string.
    /// </summary>
    /// <param name="providedKey">The retrieved API key.</param>
    /// <returns>True if the API key was found; otherwise, false.</returns>
    private bool TryGetApiKey(out string providedKey)
    {
        providedKey = string.Empty;

        // Primary header
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var values))
        {
            // Additional headers
            foreach (var header in Options.AdditionalHeaderNames)
            {
                if (Request.Headers.TryGetValue(header, out values))
                {
                    break;
                }
            }
        }

        // Query string fallback
        if ((values.Count == 0 || StringValues.IsNullOrEmpty(values))
            && Options.AllowQueryStringFallback
            && Request.Query.TryGetValue(Options.HeaderName, out var qsValues))
        {
            values = qsValues;
        }

        if (StringValues.IsNullOrEmpty(values))
        {
            return false;
        }

        // Normalize potential whitespace/newlines from transport
        providedKey = values.ToString().Trim();
        return true;
    }

    /// <summary>
    /// Validates the provided API key against the expected key or a custom validation method.
    /// </summary>
    /// <param name="providedKey">The API key provided by the client.</param>
    /// <param name="providedKeyBytes">The byte representation of the provided API key.</param>
    /// <returns>True if the API key is valid; otherwise, false.</returns>
    private async Task<bool> ValidateApiKeyAsync(string providedKey, byte[] providedKeyBytes)
    {
        return Options.ExpectedKeyBytes is not null
            ? FixedTimeEquals.Test(providedKeyBytes, Options.ExpectedKeyBytes)
            : Options.ValidateKeyAsync is not null
            ? await Options.ValidateKeyAsync(Context, providedKey, providedKeyBytes)
            : throw new InvalidOperationException(
            "No API key validation configured. Either set ValidateKey or ExpectedKey in ApiKeyAuthenticationOptions.");
    }

    /// <summary>
    /// Creates an authentication ticket for the provided API key.
    /// </summary>
    /// <param name="providedKey"></param>
    /// <returns></returns>
    private Task<AuthenticationTicket> CreateTicketAsync(string providedKey)
        => IAuthHandler.GetAuthenticationTicketAsync(Context, providedKey, Options, Scheme, "ApiKeyClient");

    /// <summary>
    /// Fails the authentication process with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>An <see cref="AuthenticateResult"/> indicating the failure.</returns>
    private AuthenticateResult Fail(string reason)
    {
        Options.Logger.Warning("API Key authentication failed: {Reason}", reason);
        return AuthenticateResult.Fail(reason);
    }

    /// <summary>
    /// Handles the authentication challenge by setting the appropriate response headers and status code.
    /// </summary>
    /// <param name="properties">Authentication properties for the challenge.</param>
    /// <returns>A completed task.</returns>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Options.EmitChallengeHeader)
        {
            var header = Options.HeaderName ?? "X-Api-Key";

            var value = Options.ChallengeHeaderFormat == ApiKeyChallengeFormat.ApiKeyHeader
                ? $"ApiKey header=\"{header}\""
                : header;

            Response.Headers.WWWAuthenticate = value;
        }
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
    /// <summary>
    /// Builds a PowerShell-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="settings">The settings containing the PowerShell authentication code.</param>
    /// <param name="logger">The logger to use for debug output.</param>
    /// <returns>A delegate that validates an API key using PowerShell code.</returns>
    /// <remarks>
    ///  This method compiles the PowerShell script and returns a delegate that can be used to validate API keys.
    /// </remarks>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildPsValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("BuildPsValidator  settings: {Settings}", settings);
        }

        return async (ctx, providedKey, providedKeyBytes) =>
               {
                   return await IAuthHandler.ValidatePowerShellAsync(settings.Code, ctx, new Dictionary<string, string>
                   {
                    { "providedKey", providedKey },
                    { "providedKeyBytes", Convert.ToBase64String(providedKeyBytes) }
                   }, logger);
               };
    }

    /// <summary>
    /// Builds a C#-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="settings">The settings containing the C# authentication code.</param>
    /// <param name="logger">The logger to use for debug output.</param>
    /// <returns>A delegate that validates an API key using C# code.</returns>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildCsValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("BuildCsValidator  settings: {Settings}", settings);
        }
        // pass the settings to the core C# validator
        var core = IAuthHandler.BuildCsValidator(
            settings,
            logger,
            ("providedKey", string.Empty), ("providedKeyBytes", Array.Empty<byte>())
            ) ?? throw new InvalidOperationException("Failed to build C# validator delegate from provided settings.");
        return (ctx, providedKey, providedKeyBytes) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["providedKey"] = providedKey,
                ["providedKeyBytes"] = providedKeyBytes
            });
    }

    /// <summary>
    /// Builds a VB.NET-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="settings">The settings containing the VB.NET authentication code.</param>
    /// <param name="logger">The logger to use for debug output.</param>
    /// <returns>A delegate that validates an API key using VB.NET code.</returns>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildVBNetValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("BuildVBNetValidator  settings: {Settings}", settings);
        }
        // pass the settings to the core VB.NET validator
        var core = IAuthHandler.BuildVBNetValidator(
            settings,
            logger,
            ("providedKey", string.Empty), ("providedKeyBytes", Array.Empty<byte>())
            ) ?? throw new InvalidOperationException("Failed to build VB.NET validator delegate from provided settings.");
        return (ctx, providedKey, providedKeyBytes) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["providedKey"] = providedKey,
                ["providedKeyBytes"] = providedKeyBytes
            });
    }
}
