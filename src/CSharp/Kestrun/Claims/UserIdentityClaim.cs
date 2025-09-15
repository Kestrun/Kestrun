using System.Security.Claims;

namespace Kestrun.Claims;
/// <summary>
/// Represents the set of claims supported by Kestrun for authentication and authorization purposes.
/// </summary> 
public enum UserIdentityClaim
{
    /// <summary>
    /// Specifies the actor claim, representing the identity of the acting party.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/actor
    /// </summary>
    Actor,
    /// <summary>
    /// Specifies the anonymous claim, representing an unauthenticated user.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/anonymous
    /// </summary>
    Anonymous,
    /// <summary>
    /// Specifies the authentication claim, representing the authentication method used.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/authentication
    /// </summary>
    Authentication,
    /// <summary>
    /// Specifies the authentication instant claim, representing the time at which the user was authenticated.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationinstant
    /// </summary>
    AuthenticationInstant,
    /// <summary>
    /// Specifies the authentication method claim, representing the method used to authenticate the user.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationmethod
    /// </summary>
    AuthenticationMethod,
    /// <summary>
    /// Specifies the authorization decision claim, representing the decision made by the authorization system.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/authorizationdecision
    /// </summary>
    AuthorizationDecision,
    /// <summary>
    /// Specifies the country claim, representing the country of the user.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/country
    /// </summary>
    Country,
    /// <summary>
    /// Specifies the date of birth claim, representing the user's date of birth.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/dateofbirth
    /// </summary>
    DateOfBirth,
    /// <summary>
    /// Specifies the DNS claim, representing the DNS name of the user.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/dns
    /// </summary>
    Dns,
    /// <summary>
    /// Specifies the deny only primary group SID claim, representing a security identifier for a group.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/denyonlyprimarygroupsid
    /// </summary>
    DenyOnlyPrimaryGroupSid,
    /// <summary>
    /// Specifies the deny only primary sid claim, representing a security identifier for a user.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/denyonlyprimarysid
    /// </summary>
    DenyOnlyPrimarySid,
    /// <summary>
    /// Specifies the deny only sid claim, representing a security identifier.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/denyonlysid
    /// </summary>
    DenyOnlySid,
    /// <summary>
    /// Specifies the deny only Windows device group claim, representing a security identifier for a Windows device group.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/denyonlywindowsdevicegroup
    /// </summary>
    DenyOnlyWindowsDeviceGroup,
    /// <summary>
    /// Specifies the email claim, representing the user's email address.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/email
    /// </summary>
    Email,
    /// <summary>
    /// Specifies the email address claim, representing the user's email address.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress
    /// </summary>
    EmailAddress,
    /// <summary>
    /// Specifies the expiration claim, representing the expiration time of the token.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/expiration
    /// </summary>
    Expiration,
    /// <summary>
    /// Specifies the given name claim, representing the user's first name.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname
    /// </summary>
    GivenName,
    /// <summary>
    /// Specifies the gender claim, representing the user's gender.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/gender
    /// </summary>
    Gender,
    /// <summary>
    /// Specifies the group SID claim, representing the security identifier for a group.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/groupsid
    /// </summary>
    GroupSid,
    /// <summary>
    /// Specifies the hash claim, representing a hash of the user's data.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/hash
    /// </summary>
    Hash,
    /// <summary>
    /// Specifies the home phone claim, representing the user's home phone number.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/homephone
    /// </summary>
    HomePhone,
    /// <summary>
    /// Specifies the is persistent claim, representing whether the user's session is persistent.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/ispersistent
    /// </summary>
    IsPersistent,
    /// <summary>
    /// Specifies the issuer claim, representing the issuer of the token.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/issuer
    /// </summary>
    Issuer,
    /// <summary>
    /// Specifies the locality claim, representing the user's locality.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/locality
    /// </summary>
    Locality,
    /// <summary>
    /// Specifies the mobile phone claim, representing the user's mobile phone number.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/mobilephone
    /// </summary>
    MobilePhone,
    /// <summary>
    /// Specifies the name claim, representing the user's full name.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name
    /// </summary>
    Name,
    /// <summary>
    /// Specifies the name identifier claim, representing the user's unique identifier.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier
    /// </summary>
    NameIdentifier,
    /// <summary>
    /// Specifies the other phone claim, representing the user's other phone number.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/otherphone
    /// </summary>
    OtherPhone,
    /// <summary>
    /// Specifies the postal code claim, representing the user's postal code.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/postalcode
    /// </summary>
    PostalCode,
    /// <summary>
    /// Specifies the primary group SID claim, representing the primary security identifier for a group.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/primarygroupsid
    /// </summary>
    PrimaryGroupSid,
    /// <summary>
    /// Specifies the personal identifier claim, representing the user's personal identifier.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/ppid
    /// </summary>
    Ppid,
    /// <summary>
    /// Specifies the private personal identifier claim, representing the user's private personal identifier.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/privateppid
    /// </summary>
    PrivatePpid,
    /// <summary>
    /// Specifies the role claim, representing the user's role in the system.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/role
    /// </summary>
    Role,
    /// <summary>
    /// Specifies the RSA claim, representing the user's RSA public key.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/rsa
    /// </summary>
    Rsa,
    /// <summary>
    /// Specifies the serial number claim, representing the user's serial number.
    /// http://schemas.microsoft.com/ws/2008/06/identity/claims/serialnumber
    /// </summary>
    SerialNumber,
    /// <summary>
    /// Specifies the sid claim, representing the security identifier for a user.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/sid
    /// </summary>
    Sid,
    /// <summary>
    /// Specifies the state or province claim, representing the user's state or province.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/stateorprovince
    /// </summary>
    StateOrProvince,
    /// <summary>
    /// Specifies the service principal name (SPN) claim, representing the SPN for the user.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/spn
    /// </summary>
    Spn,
    /// <summary>
    /// Specifies the street address claim, representing the user's street address.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/streetaddress
    /// </summary>
    StreetAddress,
    /// <summary>
    /// Specifies the surname claim, representing the user's last name.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname
    /// </summary>
    Surname,
    /// <summary>
    /// Specifies the thumbprint claim, representing the user's certificate thumbprint.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/thumbprint
    /// </summary>
    Thumbprint,
    /// <summary>
    /// Specifies the user data claim, representing additional user data.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/userdata
    /// </summary>
    UserData,
    /// <summary>
    /// Specifies the upn claim, representing the user's User Principal Name.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn
    /// </summary>
    Upn,
    /// <summary>
    /// Specifies the URI claim, representing a URI associated with the user.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/uri
    /// </summary>
    Uri,
    /// <summary>
    /// Specifies the version claim, representing the version of the user's data.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/version
    /// </summary>
    Version,
    /// <summary>
    /// Specifies the webpage claim, representing the user's webpage.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/webpage
    /// </summary>
    Webpage,
    /// <summary>
    /// Specifies the system claim, representing the user's system information.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/system
    /// </summary>
    System,
    /// <summary>
    /// Specifies the Windows account name claim, representing the user's Windows account name.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsaccountname
    /// </summary>
    WindowsAccountName,
    /// <summary>
    /// Specifies the Windows device claim, representing the user's Windows device information.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsdevice
    /// </summary>
    WindowsDevice,
    /// <summary>
    /// Specifies the Windows device group claim, representing the user's Windows device group information.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsdevicegroup
    /// </summary>
    WindowsDeviceGroup,
    /// <summary>
    /// Specifies the Windows FQBN version claim, representing the version of the Windows device.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsfqbnversion
    /// </summary>
    WindowsFqbnVersion,
    /// <summary>
    /// Specifies the Windows group SID claim, representing the security identifier for a Windows group.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsgroupsid
    /// </summary>
    WindowsGroupSid,
    /// <summary>
    /// Specifies the Windows group claim, representing the user's Windows group information.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsgroup
    /// </summary>
    WindowsGroup,
    /// <summary>
    /// Specifies the Windows device claim, representing the user's Windows device information.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsdeviceClaim
    /// </summary>    
    WindowsDeviceClaim,
    /// <summary>
    /// Specifies the Windows sub-authority claim, representing a sub-authority in the Windows security identifier.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowssubauthority
    /// </summary>
    WindowsSubAuthority,
    /// <summary>
    /// Specifies the Windows SID claim, representing the security identifier for a Windows user.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsid
    /// </summary>
    WindowsSid,
    /// <summary>
    /// Specifies the primary SID claim, representing the primary security identifier for a user.
    /// http://schemas.xmlsoap.org/ws/2005/05/identity/claims/primarysid
    /// </summary>
    PrimarySid
}

/// <summary>
/// Provides extension methods for the KestrunClaim enum.
/// </summary>
public static class KestrunClaimExtensions
{
    private static readonly Dictionary<UserIdentityClaim, string> _map = new()
    {
        [UserIdentityClaim.Actor] = ClaimTypes.Actor,
        [UserIdentityClaim.Anonymous] = ClaimTypes.Anonymous,
        [UserIdentityClaim.Authentication] = ClaimTypes.Authentication,
        [UserIdentityClaim.AuthenticationInstant] = ClaimTypes.AuthenticationInstant,
        [UserIdentityClaim.AuthenticationMethod] = ClaimTypes.AuthenticationMethod,
        [UserIdentityClaim.AuthorizationDecision] = ClaimTypes.AuthorizationDecision,
        [UserIdentityClaim.Country] = ClaimTypes.Country,
        [UserIdentityClaim.DateOfBirth] = ClaimTypes.DateOfBirth,
        [UserIdentityClaim.Dns] = ClaimTypes.Dns,
        [UserIdentityClaim.DenyOnlyPrimaryGroupSid] = ClaimTypes.DenyOnlyPrimaryGroupSid,
        [UserIdentityClaim.DenyOnlyPrimarySid] = ClaimTypes.DenyOnlyPrimarySid,
        [UserIdentityClaim.DenyOnlySid] = ClaimTypes.DenyOnlySid,
        [UserIdentityClaim.DenyOnlyWindowsDeviceGroup] = ClaimTypes.DenyOnlyWindowsDeviceGroup,
        [UserIdentityClaim.Email] = "http://schemas.microsoft.com/ws/2008/06/identity/claims/email",
        [UserIdentityClaim.EmailAddress] = ClaimTypes.Email,
        [UserIdentityClaim.Expiration] = "http://schemas.microsoft.com/ws/2008/06/identity/claims/expiration",
        [UserIdentityClaim.GivenName] = ClaimTypes.GivenName,
        [UserIdentityClaim.Gender] = ClaimTypes.Gender,
        [UserIdentityClaim.GroupSid] = ClaimTypes.GroupSid,
        [UserIdentityClaim.Hash] = ClaimTypes.Hash,
        [UserIdentityClaim.HomePhone] = ClaimTypes.HomePhone,
        [UserIdentityClaim.IsPersistent] = ClaimTypes.IsPersistent,
        [UserIdentityClaim.Issuer] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/issuer",
        [UserIdentityClaim.Locality] = ClaimTypes.Locality,
        [UserIdentityClaim.MobilePhone] = ClaimTypes.MobilePhone,
        [UserIdentityClaim.Name] = ClaimTypes.Name,
        [UserIdentityClaim.NameIdentifier] = ClaimTypes.NameIdentifier,
        [UserIdentityClaim.OtherPhone] = ClaimTypes.OtherPhone,
        [UserIdentityClaim.PostalCode] = ClaimTypes.PostalCode,
        [UserIdentityClaim.PrimaryGroupSid] = ClaimTypes.PrimaryGroupSid,
        [UserIdentityClaim.Ppid] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/ppid",
        [UserIdentityClaim.PrivatePpid] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/privateppid",
        [UserIdentityClaim.Role] = ClaimTypes.Role,
        [UserIdentityClaim.Rsa] = ClaimTypes.Rsa,
        [UserIdentityClaim.SerialNumber] = ClaimTypes.SerialNumber,
        [UserIdentityClaim.Sid] = ClaimTypes.Sid,
        [UserIdentityClaim.StateOrProvince] = ClaimTypes.StateOrProvince,
        [UserIdentityClaim.Spn] = ClaimTypes.Spn,
        [UserIdentityClaim.StreetAddress] = ClaimTypes.StreetAddress,
        [UserIdentityClaim.Surname] = ClaimTypes.Surname,
        [UserIdentityClaim.Thumbprint] = ClaimTypes.Thumbprint,
        [UserIdentityClaim.UserData] = ClaimTypes.UserData,
        [UserIdentityClaim.Upn] = ClaimTypes.Upn,
        [UserIdentityClaim.Uri] = ClaimTypes.Uri,
        [UserIdentityClaim.Version] = ClaimTypes.Version,
        [UserIdentityClaim.Webpage] = ClaimTypes.Webpage,
        [UserIdentityClaim.System] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/system",
        [UserIdentityClaim.WindowsAccountName] = ClaimTypes.WindowsAccountName,
        [UserIdentityClaim.WindowsDevice] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsdevice",
        [UserIdentityClaim.WindowsDeviceGroup] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsdevicegroup",
        [UserIdentityClaim.WindowsFqbnVersion] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsfqbnversion",
        [UserIdentityClaim.WindowsGroupSid] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsgroupsid",
        [UserIdentityClaim.WindowsGroup] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsgroup",
        [UserIdentityClaim.WindowsDeviceClaim] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsdeviceClaim",
        [UserIdentityClaim.WindowsSubAuthority] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowssubauthority",
        [UserIdentityClaim.WindowsSid] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/windowsid",
        [UserIdentityClaim.PrimarySid] = ClaimTypes.PrimarySid
    };

    /// <summary>
    /// Converts the specified <see cref="UserIdentityClaim"/> to its corresponding claim URI string.
    /// </summary>
    /// <param name="claim">The claim to convert.</param>
    /// <returns>The URI string representation of the claim.</returns>
    public static string ToClaimUri(this UserIdentityClaim claim)
        => _map[claim];
}

