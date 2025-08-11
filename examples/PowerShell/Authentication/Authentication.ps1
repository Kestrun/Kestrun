
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param()
<#
    .SYNOPSIS
        Kestrun PowerShell Example: Multi Routes
    .DESCRIPTION
        This script demonstrates how to define multiple routes in Kestrun, a PowerShell web server framework.
    .EXAMPLE
        .\Authentication.ps1
        This example shows how to set up a Kestrun server with multiple authentication methods, including Basic Authentication, API Key Authentication, and JWT Bearer Token Authentication.
        It also demonstrates how to secure routes using these authentication methods.

        $creds   = "admin:password"
        $basic   = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))

        Invoke-RestMethod -Uri https://localhost:5001/secure/ps/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/cs/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/vb/hello -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/ps/policy -Method PUT -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5001/secure/vb/policy -Method PUT -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod -Uri https://localhost:5001/secure/key/simple/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5001/secure/key/ps/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5001/secure/key/cs/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5001/secure/key/vb/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }

        Invoke-RestMethod -Uri https://localhost:5001/secure/key/ps/policy -Method GET -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5001/secure/key/ps/policy -Method DELETE -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5001/secure/key/ps/policy -Method POST -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5001/secure/key/ps/policy -Method PUT -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }

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
    #>

try {
    # Get the path of the current script
    # This allows the script to be run from any location
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    # Determine the script path and Kestrun module path
    $powerShellExamplesPath = (Split-Path -Parent ($ScriptPath))
    # Determine the script path and Kestrun module path
    $examplesPath = (Split-Path -Parent ($powerShellExamplesPath))
    # Get the parent directory of the examples path
    # This is useful for locating the Kestrun module
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    # Construct the path to the Kestrun module
    # This assumes the Kestrun module is located in the src/PowerShell/Kestr
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"
    # Import the Kestrun module from the source path if it exists, otherwise from installed modules
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    }
    else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
}
catch {
    Write-Error "Failed to import Kestrun module: $_"
    exit 1
}
$logger = New-KrLogger  |
Set-KrMinimumLevel -Value Debug  |
Add-KrSinkFile -Path ".\logs\Authentication.log" -RollingInterval Hour |
Add-KrSinkConsole |
Register-KrLogger   -Name "DefaultLogger" -PassThru -SetAsDefault

New-KrServer -Name "Kestrun Authentication"

if (Test-Path "$ScriptPath\devcert.pfx" ) {
    $cert = Import-KsCertificate -FilePath ".\devcert.pfx" -Password (convertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}
else {
    $cert = New-KsSelfSignedCertificate -DnsName 'localhost' -Exportable
    Export-KsCertificate -Certificate $cert `
        -FilePath "$ScriptPath\devcert" -Format pfx -IncludePrivateKey -Password (convertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}

if (-not (Test-KsCertificate -Certificate $cert )) {
    Write-Error "Certificate validation failed. Ensure the certificate is valid and not self-signed."
    exit 1
}
 
# Example usage:
Set-KrServerOption -DenyServerHeader
 
Set-KrServerLimit -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
# Configure the listener (adjust port, cert path, and password)
Add-KrListener -Port 5001 -IPAddress ([IPAddress]::Loopback) -X509Certificate $cert -Protocols Http1AndHttp2AndHttp3
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

Add-KrResponseCompression -EnableForHttps -MimeTypes @("text/plain", "text/html", "application/json", "application/xml", "application/x-www-form-urlencoded")
Add-KrPowerShellRuntime
Add-Favicon

# Authentication Schemes Names definitions
$BasicPowershellScheme = "PowershellBasic"
$BasicCSharpScheme = "CSharpBasic"
$BasicVBNetScheme = "VBNetBasic"
$CookieScheme = "Cookies"
$JwtScheme = "Bearer"
$ApiKeySimple = "ApiKeySimple"
$ApiKeyPowerShell = "ApiKeyPowerShell"
$ApiKeyCSharp = "ApiKeyCSharp"
$ApiKeyVBNet = "ApiKeyVBNet"
$issuer = "KestrunApi"
$audience = "KestrunClients"

$incremental = [System.Collections.Concurrent.ConcurrentDictionary[string, int]]::New([System.StringComparer]::OrdinalIgnoreCase)
$incremental['Count'] = 0

# Claim Policies
$claimConfig = New-KrClaimPolicy |
Add-KrClaimPolicy -PolicyName "CanCreate" -ClaimType "can_create" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "CanDelete" -ClaimType "can_delete" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "CanRead" -ClaimType "can_read" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "CanWrite" -ClaimType "can_write" -AllowedValues "true" |
Add-KrClaimPolicy -PolicyName "Admin" -UserClaimType Role -AllowedValues "admin" |
Build-KrClaimPolicy

# ── BASIC AUTHENTICATION ────────────────────────────────────────────────
Add-KrBasicAuthentication -Name $BasicPowershellScheme -Realm "Power-Kestrun" -AllowInsecureHttp -ScriptBlock {
    param($Username, $Password)
    write-KrInformationLog -MessageTemplate "Basic Authentication: User {0} is trying to authenticate." -PropertyValues $Username
    if ($Username -eq "admin" -and $Password -eq "password") {
        $true
    }
    else {
        $false
    }
} -IssueClaimsScriptBlock {
    param([string]$Identity)
    if ($Identity -eq 'admin') {
        # Return claims for the admin user
        return (Add-KrUserClaim -UserClaimType Role -Value "admin" |
            Add-KrUserClaim -ClaimType "can_read" -Value "true" |
            Add-KrUserClaim -ClaimType "can_write" -Value "true" |
            Add-KrUserClaim -ClaimType "can_delete" -Value "false" |
            Add-KrUserClaim -ClaimType "can_create" -Value "true")
    }
    else {
        return [System.Security.Claims.Claim[]]@()
    }
} -Logger $logger -ClaimPolicyConfig $claimConfig



Add-KrBasicAuthentication -Name $BasicCSharpScheme -Realm "CSharp-Kestrun" -AllowInsecureHttp -Code @"
   // Log.Information("Validating credentials for {Username}", username);
    return username == "admin" && password == "password";
"@ -CodeLanguage CSharp


Add-KrBasicAuthentication -Name $BasicVBNetScheme -Realm "VBNet-Kestrun" -AllowInsecureHttp -Code @"
    ' Log.Information("Validating credentials for {Username}", username)
    Return username = "admin" AndAlso password = "password"
"@ -CodeLanguage VBNet -IssueClaimsCode @"
    If Identity = "admin" Then          ' (VB is case-insensitive, but keep it consistent)
        Return New List(Of System.Security.Claims.Claim) From {
            New System.Security.Claims.Claim("can_write", "true"),
            New System.Security.Claims.Claim("can_create", "true")
        }
    End If

    ' everyone else gets no extra claims
    Return Enumerable.Empty(Of System.Security.Claims.Claim)()
"@ -IssueClaimsCodeLanguage VBNet -ClaimPolicyConfig $claimConfig -Logger $logger

# ── WINDOWS AUTHENTICATION ────────────────────────────────────────────
Add-KrWindowsAuthentication



Add-KrApiKeyAuthentication -Name $ApiKeySimple -AllowInsecureHttp -HeaderName "X-Api-Key" -ExpectedKey "my-secret-api-key"



Add-KrApiKeyAuthentication -Name $ApiKeyPowerShell -AllowInsecureHttp -HeaderName "X-Api-Key" -ScriptBlock {
    param(
        [string]$ProvidedKey
    )
    if ($ProvidedKey -eq 'my-secret-api-key') {
        return $true
    }
    else {
        return $false
    }
} -IssueClaimsCode @'
    // ← return the claims you want to add
    return new[]
    {
        new System.Security.Claims.Claim(ClaimTypes.Role, "admin"),
        new System.Security.Claims.Claim("can_create", "true")          // custom claim
    };

'@ -ClaimPolicyConfig $claimConfig -Logger $logger


Add-KrApiKeyAuthentication -Name $ApiKeyCSharp -AllowInsecureHttp -HeaderName "X-Api-Key" -Code @"
    return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key");
    // or use a simple string comparison:
    //return providedKey == "my-secret-api-key";
"@

Add-KrApiKeyAuthentication -Name $ApiKeyVBNet -AllowInsecureHttp -HeaderName "X-Api-Key" -Code @"
    Return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key")
    ' or use a simple string comparison:
    ' Return providedKey = "my-secret-api-key"
"@ -CodeLanguage VBNet


# ── JWT AUTHENTICATION ────────────────────────────────────────────────

$secretB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('my-passphrase'))  # or any base64url

$JwtKeyHex = "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0";
$JwtTokenBuilder = New-KrJWTBuilder |
Add-KrJWTIssuer    -Issuer   $issuer |
Add-KrJWTAudience  -Audience $audience |
#| Set-JwtSubject   -Subject  'admin' `
Protect-KrJWT -HexadecimalKey $JwtKeyHex -Algorithm HS256

$result = Build-KrJWT -Builder $JwtTokenBuilder
#$jwt     = Get-JwtToken -Result $result
$jwtOptions = $result | Get-KrJWTValidationParameter

Add-KrJWTBearerAuthentication -Name $JwtScheme -ValidationParameter $jwtOptions -ClaimPolicy $claimConfig


$cookie = [Microsoft.AspNetCore.Http.CookieBuilder]::new()

$cookie.Name = "KestrunAuth"
$cookie.HttpOnly = $true
$cookie.SecurePolicy = [Microsoft.AspNetCore.Http.CookieSecurePolicy]::Always
$cookie.SameSite = [Microsoft.AspNetCore.Http.SameSiteMode]::Strict

# ---- Cookies Authentication ----
Add-KrCookiesAuthentication -Name $CookieScheme -LoginPath "/cookies/login" -LogoutPath "/cookies/logout" `
    -AccessDeniedPath "/cookies/access-denied" -SlidingExpiration -expireTimeSpan (New-TimeSpan -Minutes 60) `
    -ClaimPolicy $claimConfig -Cookie $cookie

# Enable configuration
Enable-KrConfiguration

<#
***************************************************************************************
    ROUTES
    ──────────────────────────────────────────
    These routes are protected by the authentication schemes defined above.
    They will only be accessible if the user is authenticated.
****************************************************************************************
#>

# Add a route with a script block
Add-KrMapRoute -Verbs Get -Path "/secure/ps/hello" -AuthorizationSchema $BasicPowershellScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType "text/plain"
}

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/ps/policy"
        HttpVerbs       = 'Get'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_read' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanRead")
        RequireSchemes  = @($BasicPowershellScheme)
    })


Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/ps/policy"
        HttpVerbs       = 'Delete'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_delete' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanDelete")
        RequireSchemes  = @($BasicPowershellScheme)
    })

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/ps/policy"
        HttpVerbs       = 'Put'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_write' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanWrite")
        RequireSchemes  = @($BasicPowershellScheme)
    })

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/ps/policy"
        HttpVerbs       = 'Post'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_create' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanCreate")
        RequireSchemes  = @($BasicPowershellScheme)
    })


Add-KrMapRoute -Verbs Get -Path "/secure/cs/hello" -AuthorizationSchema $BasicCSharpScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType "text/plain" 
}

Add-KrMapRoute -Verbs Get -Path "/secure/vb/hello" -AuthorizationSchema $BasicVBNetScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code." -ContentType "text/plain"
}



Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/vb/policy"
        HttpVerbs       = 'Get'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_read' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanRead")
        RequireSchemes  = @($BasicVBNetScheme)
    })


Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/vb/policy"
        HttpVerbs       = 'Delete'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_delete' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanDelete")
        RequireSchemes  = @($BasicVBNetScheme)
    })

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/vb/policy"
        HttpVerbs       = 'Put'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_write' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanWrite")
        RequireSchemes  = @($BasicVBNetScheme)
    })

Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern         = "/secure/vb/policy"
        HttpVerbs       = 'Post'
        Code            = {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_create' permission." -ContentType "text/plain"
        }
        Language        = 'PowerShell'
        RequirePolicies = @("CanCreate")
        RequireSchemes  = @($BasicVBNetScheme)
    })






Add-KrMapRoute -Verbs Get -Path "/secure/key/simple/hello" -AuthorizationSchema $ApiKeySimple -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated using simple key matching." -ContentType "text/plain"
}
 

Add-KrMapRoute -Verbs Get -Path "/secure/key/ps/hello" -AuthorizationSchema $ApiKeyPowerShell -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code." -ContentType "text/plain"
}
 
Add-KrMapRoute -Verbs Get -Path "/secure/key/cs/hello" -AuthorizationSchema $ApiKeyCSharp -ScriptBlock {
 
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching C# Code." -ContentType "text/plain"
}

Add-KrMapRoute -Verbs Get -Path "/secure/key/Vb/hello" -AuthorizationSchema $ApiKeyVBNet -ScriptBlock {

    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching VB.NET Code." -ContentType "text/plain"
}


Add-KrMapRoute -Verbs Get -Path "/secure/key/ps/policy" -AuthorizationSchema $ApiKeyPowerShell -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_read' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanRead"

Add-KrMapRoute -Verbs Put -Path "/secure/key/ps/policy" -AuthorizationSchema $ApiKeyPowerShell -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_write' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanWrite"

Add-KrMapRoute -Verbs Post -Path "/secure/key/ps/policy" -AuthorizationSchema $ApiKeyPowerShell -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_create' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanCreate"

Add-KrMapRoute -Verbs Delete -Path "/secure/key/ps/policy" -AuthorizationSchema $ApiKeyPowerShell -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_delete' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanDelete"


# KESTRUN JWT AUTHENTICATION ROUTES 

Add-KrMapRoute -Verbs Get -Path "/secure/jwt/hello" -AuthorizationSchema $JwtScheme -ScriptBlock {
    Expand-KrObject $Context
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by JWT Bearer Token." -ContentType "text/plain"
}

Add-KrMapRoute -Verbs Get -Path "/secure/jwt/policy" -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_read' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanRead"

Add-KrMapRoute -Verbs Put -Path "/secure/jwt/policy" -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_write' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanWrite"

Add-KrMapRoute -Verbs Post -Path "/secure/jwt/policy" -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_create' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanCreate"

Add-KrMapRoute -Verbs Delete -Path "/secure/jwt/policy" -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_delete' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanDelete"


Add-KrMapRoute -Verbs Get -Path "/token/renew" -AuthorizationSchema $JwtScheme  -ScriptBlock {
    $user = $Context.User.Identity.Name

    write-KrInformationLog -MessageTemplate "Generating JWT token for user {0}" -PropertyValues $user
    Write-Output "JwtTokenBuilder Type : $($JwtTokenBuilder.GetType().FullName)"
    $accessToken = $JwtTokenBuilder | Update-KrJWT -FromContext
    Write-KrJsonResponse -InputObject @{
        access_token = $accessToken
        token_type   = "Bearer"
        expires_in   = $build.Expires
    } -ContentType "application/json"

}


Add-KrMapRoute -Verbs Get -Path "/token/new" -AuthorizationSchema $BasicPowershellScheme -ScriptBlock {
    $user = $Context.User.Identity.Name

    write-KrInformationLog -MessageTemplate "Regenerating JWT token for user {0}" -PropertyValues $user
    write-KrInformationLog -MessageTemplate "JWT Token Builder: {0}" -PropertyValues $JwtTokenBuilder
    if (-not $JwtTokenBuilder) {
        Write-KrErrorResponse -Message "JWT Token Builder is not initialized." -StatusCode 500
        return
    }
    Write-Output "JwtTokenBuilder Type : $($JwtTokenBuilder.GetType().FullName)"
    Write-Output "Issuer : $($JwtTokenBuilder.Issuer)"
    Write-Output "Audience : $($JwtTokenBuilder.Audience)"
    Write-Output "Algorithm: $($JwtTokenBuilder.Algorithm)"

    $build = Copy-KrJWTTokenBuilder -Builder $JwtTokenBuilder |
    Add-KrJWTSubject   -Subject $user |
    Add-KrJWTClaim -UserClaimType Name -Value $user |
    Add-KrJWTClaim -UserClaimType Role -Value "admin" |
    Add-KrJWTClaim -ClaimType "can_read" -Value "true" | Build-KrJWT
    $accessToken = $build | Get-KrJWTToken
    Write-KrJsonResponse -InputObject @{
        access_token = $accessToken
        token_type   = "Bearer"
        expires_in   = $build.Expires
    } -ContentType "application/json"

} -Arguments @{"JwtTokenBuilder" = $JwtTokenBuilder }

<#
********************************************
    Cookie authentication routes
*********************************************
#>
Add-KrMapRoute -Verbs Get -Path "/cookies/login" -ScriptBlock {
    Write-KrTextResponse -InputObject @"
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
</html>
"@ -ContentType "text/html"
}

Add-KrMapRoute -Verbs Post -Path "/cookies/login" -ScriptBlock {
    $form = $Context.Request.Form;
    if ($null -eq $form) {
        Write-KrJsonResponse -InputObject @{ success = $false; error = "Form data missing" } -ContentType "application/json"
        return;
    }
    $username = $form["username"];
    $password = $form["password"];

    if ($username -eq "admin" -and $password -eq "secret") {

        $claims = (Add-KrUserClaim -UserClaimType Name -Value $username |
            Add-KrUserClaim -UserClaimType Role -Value "admin" |
            Add-KrUserClaim -ClaimType "can_read" -Value "true" |
            Add-KrUserClaim -ClaimType "can_write" -Value "true" |
            Add-KrUserClaim -ClaimType "can_create" -Value "true")
        #Expand-KrObject $claims -Label "User Claims"
        $identity = [System.Security.Claims.ClaimsIdentity]::new( $claims, "Cookies")
        #    [System.Security.Claims.ClaimTypes]::Name,
        #   [System.Security.Claims.ClaimTypes]::Role)
        $principal = [System.Security.Claims.ClaimsPrincipal]::new($identity)
        [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignInAsync($Context.HttpContext, "Cookies", $principal).GetAwaiter().GetResult()
        Write-KrJsonResponse -InputObject @{ success = $true; message = "Login successful" }
    }
    else {
        Write-KrJsonResponse -InputObject @{ success = $false; message = "Invalid credentials." } -StatusCode 401
    }
}


Add-KrMapRoute -Verbs Get -Path "/cookies/logout" -ScriptBlock {

    [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignOutAsync($Context.HttpContext, "Cookies").Wait()
    Write-KrRedirectResponse -Location "/cookies/login"
} -AuthorizationSchema $CookieScheme



Add-KrMapRoute -Verbs Get -Path "/secure/cookies" -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies Authentication." -ContentType "text/plain"
} -AuthorizationSchema $CookieScheme


Add-KrMapRoute -Verbs Get -Path "/secure/cookies/map" -ScriptBlock {
    $incremental['Count'] = $incremental['Count'] + 1
    @{
        'BasicPowershellScheme' = $BasicPowershellScheme
        'BasicCSharpScheme'     = $BasicCSharpScheme
        'BasicVBNetScheme'      = $BasicVBNetScheme
        'CookieScheme'          = $CookieScheme
        'JwtScheme'             = $JwtScheme
        'ApiKeySimple'          = $ApiKeySimple
        'ApiKeyPowerShell'      = $ApiKeyPowerShell
        'ApiKeyCSharp'          = $ApiKeyCSharp
        'ApiKeyVBNet'           = $ApiKeyVBNet
        'Issuer'                = $issuer
        'Audience'              = $audience
        'ClaimConfig'           = $claimConfig
        'Incremental'           = $incremental
    } | Write-KrJsonResponse -ContentType "application/json"
 
} -AuthorizationSchema $CookieScheme


Add-KrMapRoute -Verbs Get -Path "secure/cookies/policy" -AuthorizationSchema $CookieScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_read' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanRead"

Add-KrMapRoute -Verbs Put -Path "secure/cookies/policy" -AuthorizationSchema $CookieScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_write' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanWrite"

Add-KrMapRoute -Verbs Post -Path "secure/cookies/policy" -AuthorizationSchema $CookieScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_create' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanCreate"

Add-KrMapRoute -Verbs Delete -Path "secure/cookies/policy" -AuthorizationSchema $CookieScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_delete' permission." -ContentType "text/plain"
} -AuthorizationPolicy "CanDelete"



<#
********************************************
# Start the server asynchronously
********************************************
#>
Start-KrServer -Server $server

