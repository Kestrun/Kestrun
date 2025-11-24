using Kestrun.Hosting;

namespace Kestrun.Authentication;

/// <summary>
/// Defines common options for authentication, including code validation, claim issuance, and claim policy configuration.
/// </summary>
public interface IAuthenticationHostOptions
{
    /// <summary>
    /// Gets or sets the logger used for authentication-related logging.
    /// </summary>
    Serilog.ILogger Logger => Host.Logger;

    /// <summary>
    /// Gets or sets the Kestrun host instance.
    /// </summary>
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
    KestrunHost Host { get; set; }
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

    /// <summary>
    /// Gets or sets the display name for the authentication scheme.
    /// </summary>
    string? DisplayName { get; set; }
}
