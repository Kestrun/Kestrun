using System.Reflection;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication.Negotiate;

namespace Kestrun.Authentication;

/// <summary>
/// Options for Windows Authentication.
/// </summary>
public class WindowsAuthOptions : NegotiateOptions, IOpenApiAuthenticationOptions, IAuthenticationHostOptions
{
    /// <inheritdoc/>
    public KestrunHost Host { get; set; } = default!;

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

    /// <inheritdoc/>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The protocol to use for Windows Authentication.
    /// </summary>
    public WindowsAuthProtocol Protocol { get; set; } = WindowsAuthProtocol.Negotiate;

    private Serilog.ILogger? _logger;
    /// <inheritdoc/>
    public Serilog.ILogger Logger
    {
        get => _logger ?? (Host is null ? Serilog.Log.Logger : Host.Logger); set => _logger = value;
    }

    /// <summary>
    /// Helper to copy values from a user-supplied WindowsAuthOptions instance to the instance
    /// </summary>
    /// <param name="target"></param>
    public void ApplyTo(WindowsAuthOptions target)
    {
        ApplyTo((NegotiateOptions)target);
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DocumentationId = DocumentationId;
        target.DisplayName = DisplayName;
        target.Host = Host;
    }

    /// <summary>
    /// Helper to copy values from a user-supplied WindowsAuthOptions instance to the instance
    /// </summary>
    /// <param name="target"></param>
    public void ApplyTo(NegotiateOptions target)
    {
        target.PersistKerberosCredentials = PersistKerberosCredentials;
        target.PersistNtlmCredentials = PersistNtlmCredentials;

        var ldapSettingsProp = typeof(NegotiateOptions)
            .GetProperty("LdapSettings", BindingFlags.Instance | BindingFlags.NonPublic);

        if (ldapSettingsProp?.GetValue(target) is object ldapSettings)
        {
            var domainProp = ldapSettings.GetType()
                .GetProperty("Domain", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var domain = domainProp?.GetValue(ldapSettings) as string;
            if (!string.IsNullOrEmpty(domain))
            {
                target.EnableLdap(domain);
            }
        }
    }
}
