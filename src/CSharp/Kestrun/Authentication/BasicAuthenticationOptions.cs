using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Kestrun.Claims;

namespace Kestrun.Authentication;

/// <summary>
/// Options for configuring Basic Authentication in Kestrun.
/// </summary>
public partial class BasicAuthenticationOptions : AuthenticationSchemeOptions, IAuthenticationCommonOptions
{
    /// <summary>
    /// Gets or sets the name of the HTTP header used for authentication.
    /// </summary>
    public string HeaderName { get; set; } = "Authorization";
    /// <summary>
    /// Gets or sets a value indicating whether the credentials are Base64 encoded.
    /// </summary>
    public bool Base64Encoded { get; set; } = true;

    /// <summary>
    /// Gets or sets the regular expression used to separate the username and password in the credentials.
    /// </summary>
    public Regex SeparatorRegex { get; set; } = MyRegex();


    /// <summary>
    /// Gets or sets the authentication realm used in the WWW-Authenticate header.
    /// </summary>
    public string Realm { get; set; } = "Kestrun";

    [GeneratedRegex("^([^:]*):(.*)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    /// <summary>
    /// Gets or sets the Serilog logger used for authentication events.
    /// </summary>
    public Serilog.ILogger Logger { get; set; } = Serilog.Log.ForContext<BasicAuthenticationOptions>();

    /// <summary>
    /// Gets or sets a value indicating whether to allow insecure HTTP connections.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to suppress the WWW-Authenticate header in responses.
    /// </summary>
    public bool SuppressWwwAuthenticate { get; set; }


    /// <summary>
    /// Delegate to validate user credentials.
    /// Parameters: HttpContext, username, password. Returns: Task&lt;bool&gt; indicating validity.
    /// </summary>
    public Func<HttpContext, string, string, Task<bool>> ValidateCredentialsAsync { get; set; } = (_, _, _) => Task.FromResult(false);

    /// <summary>
    /// Settings for the authentication code, if using a script.
    /// </summary>
    /// <remarks>
    /// This allows you to specify the language, code, and additional imports/refs.
    /// </remarks>
    public AuthenticationCodeSettings ValidateCodeSettings { get; set; } = new();

    /// <summary>
    /// After credentials are valid, this is called to add extra Claims.
    /// Parameters: HttpContext, username → IEnumerable of extra claims.
    /// </summary>
    public Func<HttpContext, string, Task<IEnumerable<Claim>>>? IssueClaims { get; set; }

    /// <summary>
    /// Settings for the claims issuing code, if using a script.
    /// </summary>
    /// <remarks>
    /// This allows you to specify the language, code, and additional imports/refs for claims issuance.
    /// </remarks>
    public AuthenticationCodeSettings IssueClaimsCodeSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the claim policy configuration.
    /// </summary>
    /// <remarks>
    /// This allows you to define multiple authorization policies based on claims.
    /// Each policy can specify a claim type and allowed values.
    /// </remarks>
    public ClaimPolicyConfig? ClaimPolicyConfig { get; set; }
    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }
    /// <inheritdoc/>
    public string? Description { get; set; }
}
