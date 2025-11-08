namespace Kestrun.Authentication;


/// <summary>
/// Defines common options for authentication, including code validation, claim issuance, and claim policy configuration.
/// </summary>
public interface IAuthenticationCommonOptions : IClaimsCommonOptions
{
    /// <summary>
    /// Settings for the authentication code, if using a script.
    /// </summary>
    /// <remarks>
    /// This allows you to specify the language, code, and additional imports/refs.
    /// </remarks>
    AuthenticationCodeSettings ValidateCodeSettings { get; set; }

    /// <summary>
    /// Gets or sets the logger used for authentication-related logging.
    /// </summary>
    Serilog.ILogger Logger { get; set; }


    /// <summary>
    /// If true, this security scheme is applied globally in OpenAPI documentation.
    /// </summary>
    bool GlobalScheme { get; set; }

    /// <summary>
    /// Optional description for the security scheme in OpenAPI documentation.
    /// </summary>
    string? Description { get; set; }
}
