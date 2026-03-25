
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Text.Json;

namespace Kestrun.Authentication;

/// <summary>
/// Options for OAuth2 authentication.
/// </summary>
public class OAuth2Options : OAuthOptions, IOpenApiAuthenticationOptions, IAuthenticationHostOptions, IOAuthCommonOptions
{
    /// <summary>
    /// Options for cookie authentication.
    /// </summary>
    public CookieAuthOptions CookieOptions { get; }

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string? DisplayName { get; set; }

    /// <inheritdoc/>
    public bool Deprecated { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

#pragma warning disable IDE0370 // Remove unnecessary suppression
    /// <inheritdoc/>
    public KestrunHost Host { get; set; } = default!;
#pragma warning restore IDE0370 // Remove unnecessary suppression

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth2Options"/> class.
    /// </summary>
    public OAuth2Options()
    {
        CookieOptions = new CookieAuthOptions()
        {
            SlidingExpiration = true
        };
    }
    /// <summary>
    /// Gets or sets the authentication scheme name.
    /// </summary>
    public string AuthenticationScheme { get; set; } = AuthenticationDefaults.OAuth2SchemeName;

    /// <summary>
    /// Gets the cookie authentication scheme name.
    /// </summary>
    public string CookieScheme =>
    CookieOptions.Cookie.Name ?? (CookieAuthenticationDefaults.AuthenticationScheme + "." + AuthenticationScheme);

    /// <summary>
    /// Configuration for claim policy enforcement.
    /// </summary>
    public ClaimPolicyConfig? ClaimPolicy { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2 authorization server metadata URL (RFC 8414).
    /// This is used for OpenAPI metadata and optional endpoint discovery.
    /// </summary>
    public string? OAuth2MetadataUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether missing OAuth2 endpoints should be
    /// resolved from <see cref="OAuth2MetadataUrl"/>.
    /// </summary>
    public bool ResolveEndpointsFromMetadata { get; set; } = false;

    private Serilog.ILogger? _logger;
    /// <inheritdoc/>
    public Serilog.ILogger Logger
    {
        get => _logger ?? (Host is null ? Serilog.Log.Logger : Host.Logger); set => _logger = value;
    }

    /// <summary>
    /// Helper to copy values from a user-supplied OAuth2Options instance to the instance
    /// created by the framework inside AddOAuth(). Reassigning the local variable (opts = source) would
    /// not work because only the local reference changes – the framework keeps the original instance.
    /// </summary>
    /// <param name="target">The target OAuth2Options instance to copy values to.</param>
    public void ApplyTo(OAuth2Options target)
    {
        ApplyTo((OAuthOptions)target);
        CookieOptions.ApplyTo(target.CookieOptions);
        // OpenAPI / documentation properties
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DisplayName = DisplayName;
        target.DocumentationId = DocumentationId;
        target.Host = Host;
        target.ClaimPolicy = ClaimPolicy;
        target.Deprecated = Deprecated;
        target.OAuth2MetadataUrl = OAuth2MetadataUrl;
        target.ResolveEndpointsFromMetadata = ResolveEndpointsFromMetadata;
    }

    /// <summary>
    /// Apply these options to the target <see cref="OAuthOptions"/> instance.
    /// </summary>
    /// <param name="target">The target OAuthOptions instance to apply settings to.</param>
    public void ApplyTo(OAuthOptions target)
    {
        // Core OAuth endpoints
        target.AuthorizationEndpoint = AuthorizationEndpoint;
        target.TokenEndpoint = TokenEndpoint;
        target.UserInformationEndpoint = UserInformationEndpoint;
        target.ClientId = ClientId;
        target.ClientSecret = ClientSecret;
        target.CallbackPath = CallbackPath;

        // OAuth configuration
        target.UsePkce = UsePkce;
        target.SaveTokens = SaveTokens;
        target.ClaimsIssuer = ClaimsIssuer;

        // Scopes - clear and copy
        target.Scope.Clear();
        foreach (var scope in Scope)
        {
            target.Scope.Add(scope);
        }

        // Token handling
        target.AccessDeniedPath = AccessDeniedPath;
        target.RemoteAuthenticationTimeout = RemoteAuthenticationTimeout;
        target.ReturnUrlParameter = ReturnUrlParameter;

        // Scheme linkage
        target.SignInScheme = SignInScheme;

        // Backchannel configuration
        if (Backchannel != null)
        {
            target.Backchannel = Backchannel;
        }
        if (BackchannelHttpHandler != null)
        {
            target.BackchannelHttpHandler = BackchannelHttpHandler;
        }
        if (BackchannelTimeout != default)
        {
            target.BackchannelTimeout = BackchannelTimeout;
        }

        // Claim actions
        if (ClaimActions != null)
        {
            foreach (var jka in ClaimActions
                .OfType<Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction>()
                .Where(a => !string.IsNullOrEmpty(a.JsonKey) && !string.IsNullOrEmpty(a.ClaimType)))
            {
                target.ClaimActions.MapJsonKey(jka.ClaimType, jka.JsonKey);
            }
        }

        // Events - copy if provided
        if (Events != null)
        {
            target.Events = Events;
        }
        if (EventsType != null)
        {
            target.EventsType = EventsType;
        }

        // Other properties
        target.StateDataFormat = StateDataFormat;
    }

    /// <summary>
    /// Populates missing OAuth2 endpoints from an OAuth2 metadata document.
    /// </summary>
    /// <param name="options">The OAuth2 options to populate.</param>
    /// <param name="httpClient">The HTTP client used to fetch metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task PopulateEndpointsFromMetadataAsync(
        OAuth2Options options,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(options.OAuth2MetadataUrl))
        {
            return;
        }

        using var json = await FetchMetadataDocumentAsync(
            options.OAuth2MetadataUrl,
            httpClient,
            cancellationToken).ConfigureAwait(false);

        if (TryResolveEndpointFromMetadata(
            options.AuthorizationEndpoint,
            json.RootElement,
            "authorization_endpoint",
            out var authorizationEndpoint))
        {
            options.AuthorizationEndpoint = authorizationEndpoint;
        }

        if (TryResolveEndpointFromMetadata(
            options.TokenEndpoint,
            json.RootElement,
            "token_endpoint",
            out var tokenEndpoint))
        {
            options.TokenEndpoint = tokenEndpoint;
        }

        if (TryResolveEndpointFromMetadata(
            options.UserInformationEndpoint,
            json.RootElement,
            "userinfo_endpoint",
            out var userInformationEndpoint))
        {
            options.UserInformationEndpoint = userInformationEndpoint;
        }
    }

    /// <summary>
    /// Downloads and parses the OAuth2 metadata document.
    /// </summary>
    /// <param name="metadataUrl">The metadata document URL.</param>
    /// <param name="httpClient">The HTTP client used to fetch metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The parsed metadata document.</returns>
    private static async Task<JsonDocument> FetchMetadataDocumentAsync(
        string metadataUrl,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(metadataUrl, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a single OAuth2 endpoint value from metadata when the current value is missing.
    /// </summary>
    /// <param name="currentEndpoint">The current endpoint value.</param>
    /// <param name="metadataRoot">The metadata JSON root element.</param>
    /// <param name="propertyName">The metadata property name to read.</param>
    /// <param name="resolvedEndpoint">The resolved endpoint, when available.</param>
    /// <returns><see langword="true"/> when an endpoint was resolved from metadata; otherwise <see langword="false"/>.</returns>
    /// <exception cref="FormatException">Thrown when a discovered endpoint is not an absolute URI.</exception>
    private static bool TryResolveEndpointFromMetadata(
        string? currentEndpoint,
        JsonElement metadataRoot,
        string propertyName,
        out string resolvedEndpoint)
    {
        resolvedEndpoint = string.Empty;

        if (!string.IsNullOrWhiteSpace(currentEndpoint))
        {
            return false;
        }

        if (!metadataRoot.TryGetProperty(propertyName, out var endpointElement) ||
            endpointElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var endpoint = endpointElement.GetString();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out _))
        {
            resolvedEndpoint = endpoint;
            return true;
        }

        throw new FormatException($"OAuth2 metadata property '{propertyName}' must be an absolute URI, but received '{endpoint}'.");
    }
}
