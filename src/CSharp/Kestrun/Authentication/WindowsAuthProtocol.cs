namespace Kestrun.Authentication;

/// <summary>
/// Defines the protocol used for Windows Authentication.
/// </summary>
public enum WindowsAuthProtocol
{
    /// <summary>
    /// Negotiate protocol (Kerberos or NTLM).
    /// </summary>
    Negotiate,
    /// <summary>
    /// NTLM protocol.
    /// </summary>
    Ntlm
}
