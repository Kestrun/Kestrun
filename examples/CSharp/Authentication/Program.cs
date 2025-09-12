using System.Net;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using System.Text;
using System.Security.Claims;
using Kestrun.Certificates;
using Kestrun.Logging;
using Kestrun.Utilities;
using Kestrun.Scripting;
using Kestrun.Hosting;
using Kestrun.Authentication;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.ResponseCompression;
using Kestrun.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Kestrun.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;          // ISecurityTokenValidator


/*
$creds   = "admin:password"
$basic   = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))

Invoke-RestMethod -Uri https://localhost:5001/secure/ps/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/cs/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/vb/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/native/hello -SkipCertificateCheck -Headers @{Authorization=$basic}

Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method PUT -SkipCertificateCheck -Headers @{Authorization=$basic}

Invoke-RestMethod -Uri https://localhost:5001/secure/native/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/native/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/native/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/native/policy -Method PUT -SkipCertificateCheck -Headers @{Authorization=$basic}

Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method PUT -SkipCertificateCheck -Headers @{Authorization=$basic}

Invoke-RestMethod -Uri https://localhost:5001/secure/key/simple/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod -Uri https://localhost:5001/secure/key/ps/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod -Uri https://localhost:5001/secure/key/cs/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod -Uri https://localhost:5001/secure/key/vb/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }


$token = (Invoke-RestMethod -Uri https://localhost:5001/token/new -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
Invoke-RestMethod -Uri https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

Invoke-RestMethod -Uri https://localhost:5001/secure/jwt/policy -Method GET -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri https://localhost:5001/secure/jwt/policy -Method DELETE -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri https://localhost:5001/secure/jwt/policy -Method POST -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri https://localhost:5001/secure/jwt/policy -Method PUT -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

$token2 = (Invoke-RestMethod -Uri https://localhost:5001/token/renew -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }).access_token
Invoke-RestMethod -Uri https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token2" }
#Form
Invoke-WebRequest -Uri https://localhost:5001/cookies/login -SkipCertificateCheck -Method Post -Body @{ username = 'admin'; password = 'secret' } -SessionVariable authSession
Invoke-WebRequest -Uri https://localhost:5001/secure/cookies -SkipCertificateCheck -WebSession $authSession
Invoke-RestMethod -Uri https://localhost:5001/secure/cookies/policy -Method GET -SkipCertificateCheck -WebSession $authSession
Invoke-RestMethod -Uri https://localhost:5001/secure/cookies/policy -Method DELETE -SkipCertificateCheck -WebSession $authSession
Invoke-RestMethod -Uri https://localhost:5001/secure/cookies/policy -Method POST -SkipCertificateCheck -WebSession $authSession
Invoke-RestMethod -Uri https://localhost:5001/secure/cookies/policy -Method PUT -SkipCertificateCheck -WebSession $authSession
*/

var currentDir = Directory.GetCurrentDirectory();

// 1️⃣  Audit log: only warnings and above, writes JSON files
Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/basic_auth_handler.log", rollingInterval: RollingInterval.Day)
        .Register("default", setAsDefault: true);

// 1. Create server
var server = new KestrunHost("Kestrun Authentication", currentDir);
// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false; // DenyServerHeader

server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;
server.Options.ServerLimits.MaxRequestHeaderCount = 100;
server.Options.ServerLimits.KeepAliveTimeout = TimeSpan.FromSeconds(120);

const string BasicPowershellScheme = "PowershellBasic";
const string BasicNativeScheme = "NativeBasic";
const string BasicCSharpScheme = "CSharpBasic";
const string BasicVBNetScheme = "VBNetBasic";
const string CookieScheme = "Cookie";
const string JwtScheme = "Bearer";
const string ApiKeySimple = "ApiKeySimple";
const string ApiKeyPowerShell = "ApiKeyPowerShell";
const string ApiKeyCSharp = "ApiKeyCSharp";
const string ApiKeyVBNet = "ApiKeyVBNet";

var issuer = "KestrunApi";
var audience = "KestrunClients";
// 1) 32-byte hex or ascii secret  (use a vault / env var in production)
// shared secret = 32-byte array

const string JwtKeyHex =
    "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0"; // 32-byte hex string
var keyBytes = Convert.FromHexString(JwtKeyHex);
var keyB64u = Base64UrlEncoder.Encode(keyBytes);   // <-- supply this
var textKey = Encoding.UTF8.GetString(keyBytes);
var tokenBuilder = JwtTokenBuilder.New()
                .WithSubject("admin")
                .WithIssuer(issuer)
                .WithAudience(audience)
                .SignWithSecret(keyB64u, JwtAlgorithm.HS256)   // GCM enc
                .ValidFor(TimeSpan.FromHours(1));


var builderResult = tokenBuilder.Build();

var claimConfig = new ClaimPolicyBuilder().
AddPolicy("CanDelete", "can_delete", "true").
AddPolicy("CanRead", "can_read", "true").
AddPolicy("CanWrite", "can_write", "true").
AddPolicy("CanCreate", "can_create", "true").
AddPolicy("Admin", UserIdentityClaim.Role, "admin").
Build();

/// Add compression
server.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/json",
        "application/bson",
        "application/cbor",
        "application/yaml",
        "application/xml",
        "text/plain",
        "text/html"
    ]);
    options.Providers.Add<BrotliCompressionProvider>();
})

/// Enable PowerShell runtime
.AddPowerShellRuntime()

/// ── BASIC AUTHENTICATION – POWERSHELL CODE ─────────────────────────────
.AddBasicAuthentication(BasicPowershellScheme, opts =>
{
    opts.Realm = "Power-Kestrun";

    opts.ValidateCodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.PowerShell,
        Code = """
            param(
                [string]$Username,
                [string]$Password
            )
            if ($Username -eq 'admin' -and $Password -eq 'password') {
                return $true
            } else {
                return $false
            }
        """
    };
    // Issue claims code settings for PowerShell
    // This code will be executed to issue claims based on the username
    // It can return an array of System.Security.Claims.Claim objects
    // or an empty array if no claims are issued.
    opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.PowerShell,
        Code = """
                param([string]$Identity)
                if ($Identity -eq 'admin') {
                    return  (Add-KrUserClaim -UserClaimType Role -Value "admin" |
                        Add-KrUserClaim -ClaimType "can_read" -Value "true" |
                        Add-KrUserClaim -ClaimType "can_write" -Value "true" |
                        Add-KrUserClaim -ClaimType "can_delete" -Value "false"|
                        Add-KrUserClaim -ClaimType "can_create" -Value "true")
                }
                else {
                    return [System.Security.Claims.Claim[]]@()
                }
             """
    };


    /*
    // Native C# code for issuing claims
    opts.NativeIssueClaims = (ctx, user) =>
    {
        if (identity == "admin")
        {
            // ← return the claims you want to add
              return new[]
              {
              new Claim("can_delete", "true")          // custom claim
              // or, if you really want it as a role:
              // new Claim(ClaimTypes.Role, "can_read")
          };
          }

          // everyone else gets no extra claims
          return Enumerable.Empty<Claim>();
      };   */
    /*   /// Issue claims code settings for C#
       /// This code will be executed to issue claims based on the username
       /// It can return an array of System.Security.Claims.Claim objects
       /// or an empty array if no claims are issued.
       opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
       {
           Language = ScriptLanguage.CSharp,
           Code = """
               if (identity == "admin")
               {
                   // ← return the claims you want to add
                   return new[]
                   {
                   new System.Security.Claims.Claim("can_delete", "true")          // custom claim
                   // or, if you really want it as a role:
                   // new Claim(ClaimTypes.Role, "can_read")
               };
               }

               // everyone else gets no extra claims
               return Enumerable.Empty<System.Security.Claims.Claim>();
           """
       };*/


    opts.Base64Encoded = true;            // default anyway
    opts.RequireHttps = false;           // example
    opts.ClaimPolicyConfig = claimConfig;
}
)

/// ── BASIC AUTHENTICATION – NATIVE C# CODE ──────────────────────────────
   .AddBasicAuthentication(BasicNativeScheme, opts =>
   {
       opts.Realm = "Native-Kestrun";
       opts.ValidateCredentialsAsync = async (_, username, password) =>
       {
           Log.Information("Validating credentials for {Username}", username);
           // pretend we did some async work:
           await Task.Yield();
           // Replace with your real credential validation logic
           return username == "admin" && password == "password";
       };
       // Issue claims code settings for PowerShell
       // This code will be executed to issue claims based on the username
       // It can return an array of System.Security.Claims.Claim objects
       // or an empty array if no claims are issued.
       opts.IssueClaims = async (_, identity) =>
       {
           if (identity == "admin")
           {
               // ← return the claims you want to add
               return
               [
                   new Claim("can_read", "true"),        // custom claim
                   new Claim("can_delete", "true")
                   // or, if you really want it as a role:
                   // new Claim(ClaimTypes.Role, "can_read")
               ];
           }
           await Task.Yield();
           // everyone else gets no extra claims
           return [];
       };
       opts.ClaimPolicyConfig = claimConfig;
   })

/// ── BASIC AUTHENTICATION – C# CODE ────────────────────────────────────
   .AddBasicAuthentication(BasicCSharpScheme, opts =>
   {
       opts.Realm = "CSharp-Kestrun";
       opts.ValidateCodeSettings = new AuthenticationCodeSettings
       {
           Language = ScriptLanguage.CSharp,

           Code = """
           return username == "admin" && password == "password";
       """
       };
   }
   )
/// ── BASIC AUTHENTICATION – C# CODE ────────────────────────────────────
   .AddBasicAuthentication(BasicVBNetScheme, opts =>
   {
       opts.Realm = "VBNet-Kestrun";
       opts.ValidateCodeSettings = new AuthenticationCodeSettings
       {
           Language = ScriptLanguage.VBNet,

           Code = """
           Return username = "admin" AndAlso password = "password"
       """
       };

       /// Issue claims code settings for C#
       /// This code will be executed to issue claims based on the username
       /// It can return an array of System.Security.Claims.Claim objects
       /// or an empty array if no claims are issued.
       opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
       {
           //   ExtraImports = new[] { "System.Collections.Generic", "System.Linq" },
           Language = ScriptLanguage.VBNet,
           Code = """
               If Identity = "admin" Then          ' (VB is case-insensitive, but keep it consistent)
                    Return New List(Of Claim) From {
                        New Claim("can_write", "true"),
                        New Claim("can_create", "true")
                    }
                End If

                ' everyone else gets no extra claims
                Return Enumerable.Empty(Of Claim)()
           """
       };
       opts.ClaimPolicyConfig = claimConfig;
   })
/// ── WINDOWS AUTHENTICATION ────────────────────────────────────────────
   .AddWindowsAuthentication()

/// ── API KEY AUTHENTICATION – SIMPLE STRING MATCHING ────────────────────
   .AddApiKeyAuthentication(ApiKeySimple, opts =>
   {
       opts.HeaderName = "X-Api-Key";
       opts.ExpectedKey = "my-secret-api-key";
   })

/// ── API KEY AUTHENTICATION – POWERSHELL CODE ───────────────────────────
   .AddApiKeyAuthentication(ApiKeyPowerShell, opts =>
   {
       opts.HeaderName = "X-Api-Key";
       opts.ValidateCodeSettings = new AuthenticationCodeSettings
       {
           Language = ScriptLanguage.PowerShell,
           Code = """
       param(
           [string]$ProvidedKey
       )
       if ($ProvidedKey -eq 'my-secret-api-key') {
           return $true
       } else {
           return $false
       }
       """
       };
       /// Issue claims code settings for C#
       /// This code will be executed to issue claims based on the username
       /// It can return an array of System.Security.Claims.Claim objects
       /// or an empty array if no claims are issued.
       opts.IssueClaimsCodeSettings = new AuthenticationCodeSettings
       {
           Language = ScriptLanguage.CSharp,
           Code = """
               if (identity == "admin")
               {
                   // ← return the claims you want to add
                   return new[]
                   {
                   new System.Security.Claims.Claim("can_read", "true")          // custom claim
                   // or, if you really want it as a role:
                   // new Claim(ClaimTypes.Role, "can_read")
               };
               }

               // everyone else gets no extra claims
               return Enumerable.Empty<System.Security.Claims.Claim>();
           """
       };
       opts.ClaimPolicyConfig = claimConfig;
   })
/// ── API KEY AUTHENTICATION – C# CODE ───────────────────────────────────
   .AddApiKeyAuthentication(ApiKeyCSharp, opts =>
   {
       opts.HeaderName = "X-Api-Key";
       opts.ValidateCodeSettings = new AuthenticationCodeSettings
       {
           Language = ScriptLanguage.CSharp,
           Code = """
       return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key");
       // or use a simple string comparison: // or use a simple string comparison:
       // return providedKey == "my-secret-api-key";
       """,
           ExtraImports = ["System.Text"]
       };
   })
   .AddApiKeyAuthentication(ApiKeyVBNet, opts =>
   {
       opts.HeaderName = "X-Api-Key";
       opts.ValidateCodeSettings = new AuthenticationCodeSettings
       {
           Language = ScriptLanguage.VBNet,
           Code = """
       Return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key")
       ' or use a simple string comparison:
       ' Return providedKey = "my-secret-api-key"
       """,
           ExtraImports = ["System.Text"]
       };
   })
/// ── JWT AUTHENTICATION – C# CODE ───────────────────────────────────────
   .AddJwtBearerAuthentication(
       scheme: JwtScheme,
        validationParameters: builderResult.GetValidationParameters(),
        claimPolicy: claimConfig
    )

/// ── COOKIE AUTHENTICATION – C# CODE ────────────────────────────────────
/*
.AddCookieAuthentication(
    scheme: "Cookies",
    configure: opts =>
    {
        opts.Cookie.Name = "Kestrun.Cookie";
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opts.LoginPath = "/cookies/login";
        opts.LogoutPath = "/cookies/logout";
        opts.AccessDeniedPath = "/cookies/access-denied";
        opts.SlidingExpiration = true;
        opts.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        opts.Cookie.SameSite = SameSiteMode.Strict;
    },
      claimPolicy: claimConfig
);*/
.AddCookieAuthentication(
       scheme: CookieScheme,
       configure: new CookieAuthenticationOptions
       {
           Cookie = new CookieBuilder
           {
               Name = "Kestrun.Cookie",
               HttpOnly = true,
               SecurePolicy = CookieSecurePolicy.Always,
               SameSite = SameSiteMode.Strict
           },
           LoginPath = "/cookies/login",
           LogoutPath = "/cookies/logout",
           AccessDeniedPath = "/cookies/access-denied",
           SlidingExpiration = true,
           ExpireTimeSpan = TimeSpan.FromMinutes(60),
       },
         claimPolicy: claimConfig
   );
//***************************************************************************************
//    CERTIFICATE IMPORT/CREATION
//    ──────────────────────────────────────────
//    This section handles the creation or import of a self-signed certificate.
//****************************************************************************************
X509Certificate2? x509Certificate = null;

if (File.Exists("./devcert.pfx"))
{
    // Import existing certificate
    x509Certificate = CertificateManager.Import(
      "./devcert.pfx",
      "p@ss".AsSpan()
  );
}
else
{
    // Create a new self-signed certificate
    x509Certificate = CertificateManager.NewSelfSigned(
      new CertificateManager.SelfSignedOptions(
          DnsNames: ["localhost", "127.0.0.1"],
          KeyType: CertificateManager.KeyType.Rsa,
          KeyLength: 2048,
          ValidDays: 30,
          Exportable: true
      )
  );
    // Export the certificate to a file
    CertificateManager.Export(
        x509Certificate,
        "./devcert.pfx",
        CertificateManager.ExportFormat.Pfx,
        "p@ss".AsSpan()
    );
}

if (!CertificateManager.Validate(
    x509Certificate,
    checkRevocation: false,
    allowWeakAlgorithms: false,
    denySelfSigned: false,
    strictPurpose: true
))
{
    Console.WriteLine("Certificate validation failed.");
    Environment.Exit(1);
}



// 2. Configure Kestrel server
// This will set up the server to listen on specific ports and IP addresses
// It will also configure the server to use the provided certificate for HTTPS
server.ConfigureListener(
    port: 5001,
    ipAddress: IPAddress.Loopback,
    x509Certificate: x509Certificate,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3
);
/// Configure HTTP listener
/// This will set up the server to listen on a specific port and IP address
/// It will also configure the server to use HTTP/1.1 protocol
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Loopback,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

// 3. Enable configuration
// This will load configuration from appsettings.json and environment variables
// It will also enable Serilog configuration from appsettings.json
server.EnableConfiguration();


//***************************************************************************************
//    ROUTES
//    ──────────────────────────────────────────
//    These routes are protected by the authentication schemes defined above.
//    They will only be accessible if the user is authenticated.
//****************************************************************************************

//*********************************************
//    BASIC AUTHENTICATION ROUTES
//**********************************************
server.AddMapRoute("/secure/ps/hello", HttpVerb.Get, """
       $user = $Context.User.Identity.Name
       Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType "text/plain"
   """, ScriptLanguage.PowerShell, [BasicPowershellScheme]);


server.AddMapRoute(new()
{
    Pattern = "/secure/ps/policy",
    HttpVerbs = [HttpVerb.Get],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_read' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanRead"],
    RequireSchemes = [BasicPowershellScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/ps/policy",
    HttpVerbs = [HttpVerb.Delete],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_delete' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanDelete"],
    RequireSchemes = [BasicPowershellScheme]
});


server.AddMapRoute(new()
{
    Pattern = "/secure/ps/policy",
    HttpVerbs = [HttpVerb.Put],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_write' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanWrite"],
    RequireSchemes = [BasicPowershellScheme]
});
server.AddMapRoute(new()
{
    Pattern = "/secure/ps/policy",
    HttpVerbs = [HttpVerb.Post],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_create' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanCreate"],
    RequireSchemes = [BasicPowershellScheme]
});


server.AddMapRoute("/secure/cs/hello", HttpVerb.Get, """

       $user = $Context.User.Identity.Name
       Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType "text/plain"
   """, ScriptLanguage.PowerShell, [BasicCSharpScheme]);


server.AddMapRoute("/secure/vb/hello", HttpVerb.Get, """
       $user = $Context.User.Identity.Name
       Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code." -ContentType "text/plain"
   """, ScriptLanguage.PowerShell, [BasicVBNetScheme]);


server.AddMapRoute(new()
{
    Pattern = "/secure/vb/policy",
    HttpVerbs = [HttpVerb.Get],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET code because you have the 'can_read' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanRead"],
    RequireSchemes = [BasicVBNetScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/vb/policy",
    HttpVerbs = [HttpVerb.Delete],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET code because you have the 'can_delete' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanDelete"],
    RequireSchemes = [BasicVBNetScheme]
});


server.AddMapRoute(new()
{
    Pattern = "/secure/vb/policy",
    HttpVerbs = [HttpVerb.Put],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET code because you have the 'can_write' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanWrite"],
    RequireSchemes = [BasicVBNetScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/vb/policy",
    HttpVerbs = [HttpVerb.Post],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET code because you have the 'can_create' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanCreate"],
    RequireSchemes = [BasicVBNetScheme]
});

server.AddMapRoute("/secure/native/hello", HttpVerb.Get, """
       if (-not $Context.User.Identity.IsAuthenticated) {
           Write-KrErrorResponse -Message "Access denied" -StatusCode 401
           return
       }

       $user = $Context.User.Identity.Name
       Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native C# code." -ContentType "text/plain"
   """, ScriptLanguage.PowerShell, [BasicNativeScheme]);


server.AddMapRoute(new()
{
    Pattern = "/secure/native/policy",
    HttpVerbs = [HttpVerb.Get],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native C# code because you have the 'can_read' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanRead"],
    RequireSchemes = [BasicNativeScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/native/policy",
    HttpVerbs = [HttpVerb.Delete],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native C# code because you have the 'can_delete' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanDelete"],
    RequireSchemes = [BasicNativeScheme]
});


server.AddMapRoute(new()
{
    Pattern = "/secure/native/policy",
    HttpVerbs = [HttpVerb.Put],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native C# code because you have the 'can_write' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanWrite"],
    RequireSchemes = [BasicNativeScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/native/policy",
    HttpVerbs = [HttpVerb.Post],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native C# code because you have the 'can_create' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanCreate"],
    RequireSchemes = [BasicNativeScheme]
});


/*
********************************************
    API KEY AUTHENTICATION ROUTES
*********************************************
*/
server.AddMapRoute("/secure/key/simple/hello", HttpVerb.Get, async (ctx) =>
{
    var user = ctx.User?.Identity?.Name;
    await ctx.Response.WriteTextResponseAsync($"Welcome, {user}! You are authenticated using simple key matching.", 200);
}, [ApiKeySimple]);


server.AddMapRoute("/secure/key/ps/hello", HttpVerb.Get, """
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code."

""", ScriptLanguage.PowerShell, [ApiKeyPowerShell]);


server.AddMapRoute("/secure/key/cs/hello", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated by C# code.", 200);
""", ScriptLanguage.CSharp, [ApiKeyCSharp]);

server.AddMapRoute("/secure/key/vb/hello", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated by VB.NET code.", 200);
""", ScriptLanguage.CSharp, [ApiKeyVBNet]);

/* KESTRUN JWT AUTHENTICATION ROUTES */
server.AddMapRoute("/secure/jwt/cs/hello", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated.", 200);
""", ScriptLanguage.CSharp, [JwtScheme]);

server.AddMapRoute("/secure/jwt/hello", HttpVerb.Get, """
     Expand-KrObject $Context
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by JWT Bearer Token." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [JwtScheme]);


server.AddMapRoute(new()
{
    Pattern = "/secure/jwt/policy",
    HttpVerbs = [HttpVerb.Get],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_read' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanRead"],
    RequireSchemes = [JwtScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/jwt/policy",
    HttpVerbs = [HttpVerb.Delete],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_delete' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanDelete"],
    RequireSchemes = [JwtScheme]
});


server.AddMapRoute(new()
{
    Pattern = "/secure/jwt/policy",
    HttpVerbs = [HttpVerb.Put],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_write' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanWrite"],
    RequireSchemes = [JwtScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/jwt/policy",
    HttpVerbs = [HttpVerb.Post],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_create' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanCreate"],
    RequireSchemes = [JwtScheme]
});

server.AddMapRoute("/token/renew", HttpVerb.Get, async (ctx) =>
{
    var token = tokenBuilder.RenewJwt(ctx);
    if (string.IsNullOrEmpty(token))
    {
        await ctx.Response.WriteErrorResponseAsync("Failed to renew token", 400);
        return;
    }
    await ctx.Response.WriteJsonResponseAsync(new { access_token = token });
}, [JwtScheme]);


server.AddMapRoute("/token/new", HttpVerb.Get, async (ctx) =>
{
    try
    {
        var user = ctx.User.Identity?.Name ?? "admin";
        var build = tokenBuilder.CloneBuilder().WithSubject(user).AddClaim(ClaimTypes.Name, user).AddClaim(ClaimTypes.Role, "admin")
        .AddClaim("can_read", "true").Build();
        var token = build.Token();

        await ctx.Response.WriteJsonResponseAsync(new { access_token = token, token_type = "Bearer", ExpiresIn = build.Expires }); // 1 hour TTL
        Log.Information("Generated new JWT token: {Token}", token);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to generate JWT token");
        await ctx.Response.WriteErrorResponseAsync("Internal Server Error", 500);
    }
}, [BasicNativeScheme]);


/*
********************************************
    Cookie authentication routes
*********************************************
*/
server.AddMapRoute("/cookies/login", HttpVerb.Get, async ctx =>
{
    await ctx.Response.WriteTextResponseAsync(@"
<!DOCTYPE html>
<html>
  <head>
    <meta charset='utf-8' />
    <title>Login</title>
  </head>
  <body>
    <h1>Login</h1>
    <form method='post' action='/cookies/login'>
      <label>
        Username:
        <input type='text' name='username' required />
      </label><br/>
      <label>
        Password:
        <input type='password' name='password' required />
      </label><br/>
      <button type='submit'>Log In</button>
    </form>
  </body>
</html>", statusCode: 200, contentType: "text/html; charset=UTF-8");
});
server.AddMapRoute("/cookies/login", HttpVerb.Post, async (ctx) =>
{
    var form = ctx.Request.Form;
    if (form == null)
    {
        await ctx.Response.WriteJsonResponseAsync(new { success = false, error = "Form data missing" });
        return;
    }
    var username = form["username"];
    var password = form["password"];
    if (username == "admin" && password == "secret")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "admin"),
            new("can_read", "true"),
            new("can_write", "true"),
            new("can_create", "true")
        };
        var identity = new ClaimsIdentity(claims, CookieScheme);
        var principal = new ClaimsPrincipal(identity);
        await ctx.HttpContext.SignInAsync(CookieScheme, principal);
        await ctx.Response.WriteJsonResponseAsync(new { success = true });
    }
    else
    {
        await ctx.Response.WriteJsonResponseAsync(new { success = false }, statusCode: 401);
    }
});

server.AddMapRoute("/cookies/logout", HttpVerb.Get, async ctx =>
{
    await ctx.HttpContext.SignOutAsync("Cookies");
    // After logout, send them back to login
    ctx.Response.StatusCode = 302;
    ctx.Response.Headers["Location"] = "/cookies/login";
}, [CookieScheme]);
/*
server.AddMapRoute("/cookies/login2", HttpVerb.Post, """
    write-host 'Processing login request...'
    # Check if the form contains the expected fields
    Write-KrInformationLog -Message 'Received login request with form data: {@Position}' -PropertyValues $Context.Request.Form
    Write-Host "User: $($Context.Request.Form['user']), Password: $($Context.Request.Form['pass'])"
    if ($Context.Request.Form['user'] -eq 'admin' -and $Request.Form['pass'] -eq 'secret') {
        # Build claims principal
        $claims = [System.Collections.Generic.List[System.Security.Claims.Claim]]::new()
        $claims.Add([System.Security.Claims.Claim]::new('sub','admin'))
        $identity = [System.Security.Claims.ClaimsIdentity]::new($claims,'Cookies')
        $principal = [System.Security.Claims.ClaimsPrincipal]::new($identity)
        # Issue the cookie
        $Context.Request.HttpContext.SignInAsync('Cookies',$principal).Wait()
        Write-KrTextResponse -InputObject 'Logged in successfully!' -ContentType 'text/plain'
    } else {
        Write-KrErrorResponse -Message 'Unauthorized' -StatusCode 401
    }
""", ScriptLanguage.PowerShell);

server.AddMapRoute("/secure/cookies2", HttpVerb.Get, """
    # Check if the user is authenticated
    if (!$Context.User.Identity.IsAuthenticated) {
        Write-KrRedirectResponse -Location '/cookies/login'
    } else {
        Write-KrTextResponse -InputObject ('Hello, '+$Context.User.Identity.Name) -ContentType 'text/plain'
    }
  """, ScriptLanguage.PowerShell, [CookieScheme]);

server.AddMapRoute("/secure/cookies3", HttpVerb.Get, """
   if (Context.User?.Identity == null || !Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteRedirectResponse("/cookies/login");
    }
    else
    {
       await Context.Response.WriteTextResponseAsync($"Hello, {Context.User.Identity.Name}");
    }
""", ScriptLanguage.CSharp, [CookieScheme]);
*/

server.AddMapRoute("/secure/cookies", HttpVerb.Get, async (ctx) =>
{
    if (ctx.User?.Identity == null || !ctx.User.Identity.IsAuthenticated)
    {
        ctx.Response.WriteRedirectResponse("/cookies/login");
    }
    else
    {
        await ctx.Response.WriteTextResponseAsync($"Hello, {ctx.User.Identity.Name}");
    }
}, [CookieScheme]);


server.AddMapRoute(new()
{
    Pattern = "/secure/cookies/policy",
    HttpVerbs = [HttpVerb.Get],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookie checker because you have the 'can_read' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanRead"],
    RequireSchemes = [CookieScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/cookies/policy",
    HttpVerbs = [HttpVerb.Delete],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookie checker because you have the 'can_delete' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanDelete"],
    RequireSchemes = [CookieScheme]
});


server.AddMapRoute(new()
{
    Pattern = "/secure/cookies/policy",
    HttpVerbs = [HttpVerb.Put],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookie checker because you have the 'can_write' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanWrite"],
    RequireSchemes = [CookieScheme]
});

server.AddMapRoute(new()
{
    Pattern = "/secure/cookies/policy",
    HttpVerbs = [HttpVerb.Post],
    Code = """

           $user = $Context.User.Identity.Name
           Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookie checker because you have the 'can_create' permission." -ContentType "text/plain"
       """,
    Language = ScriptLanguage.PowerShell,
    RequirePolicies = ["CanCreate"],
    RequireSchemes = [CookieScheme]
});



//***************************************************************************************
//    Kerberos Authentication Route
//    ──────────────────────────────────────────
//    This route is protected by Kerberos authentication.
//    It will only be accessible if the user is authenticated via Kerberos.
//****************************************************************************************
server.AddMapRoute("/kerberos/secure", HttpVerb.Get, async (ctx) =>
{
    var userName = ctx.User?.Identity?.Name ?? "Unknown";
    await ctx.Response.WriteTextResponseAsync($"Hello, {userName}");
}, [NegotiateDefaults.AuthenticationScheme]);



//***************************************************************************************
//    Start the server
//    ──────────────────────────────────────────
//    This will start the Kestrun server and listen for incoming requests.
//****************************************************************************************

await server.RunUntilShutdownAsync(
  consoleEncoding: Encoding.UTF8,
  onStarted: () => Console.WriteLine("Server ready 🟢"),
  onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

  )
);
