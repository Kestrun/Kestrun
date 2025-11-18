# NOTE: This include mirrors the Authentication full example.
# Source reference: examples/PowerShell/Authentication/Authentication.ps1
#
# For readability in the docs, this is a straight copy of the sample.

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
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

        Invoke-RestMethod -Uri https://localhost:5000/secure/ps/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/cs/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/vb/hello -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod -Uri https://localhost:5000/secure/ps/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/ps/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/ps/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/ps/policy -Method PUT -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod -Uri https://localhost:5000/secure/vb/policy -Method GET -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/vb/policy -Method DELETE -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/vb/policy -Method POST -SkipCertificateCheck -Headers @{Authorization=$basic}
        Invoke-RestMethod -Uri https://localhost:5000/secure/vb/policy -Method PUT -SkipCertificateCheck -Headers @{Authorization=$basic}

        Invoke-RestMethod -Uri https://localhost:5000/secure/key/simple/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/key/ps/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/key/cs/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/key/vb/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }

        Invoke-RestMethod -Uri https://localhost:5000/secure/key/ps/policy -Method GET -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/key/ps/policy -Method DELETE -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/key/ps/policy -Method POST -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/key/ps/policy -Method PUT -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }

        $token = (Invoke-RestMethod -Uri https://localhost:5000/token/new -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
        Invoke-RestMethod -Uri https://localhost:5000/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

        Invoke-RestMethod -Uri https://localhost:5000/secure/jwt/policy -Method GET -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/jwt/policy -Method DELETE -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/jwt/policy -Method POST -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
        Invoke-RestMethod -Uri https://localhost:5000/secure/jwt/policy -Method PUT -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

        $token2 = (Invoke-RestMethod -Uri https://localhost:5000/token/renew -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }).access_token
        Invoke-RestMethod -Uri https://localhost:5000/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token2" }
        #Form
        Invoke-WebRequest -Uri https://localhost:5000/cookies/login -SkipCertificateCheck -Method Post -Body @{ username = 'admin'; password = 'secret' } -SessionVariable authSession
        Invoke-WebRequest -Uri https://localhost:5000/secure/cookies/hello -SkipCertificateCheck -WebSession $authSession
        Invoke-RestMethod -Uri https://localhost:5000/secure/cookies/policy -Method GET -SkipCertificateCheck -WebSession $authSession
        Invoke-RestMethod -Uri https://localhost:5000/secure/cookies/policy -Method DELETE -SkipCertificateCheck -WebSession $authSession
        Invoke-RestMethod -Uri https://localhost:5000/secure/cookies/policy -Method POST -SkipCertificateCheck -WebSession $authSession
        Invoke-RestMethod -Uri https://localhost:5000/secure/cookies/policy -Method PUT -SkipCertificateCheck -WebSession $authSession
#>

$logger = New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\Authentication.log' -RollingInterval Hour |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -PassThru -SetAsDefault

New-KrServer -Name 'Kestrun Authentication'

Initialize-KrRoot -Path $PSScriptRoot

if (Test-Path 'devcert.pfx' ) {
    $cert = Import-KrCertificate -FilePath 'devcert.pfx' -Password (ConvertTo-SecureString -String 'p@ss' -AsPlainText -Force)
} else {
    $cert = New-KrSelfSignedCertificate -DnsNames 'localhost' -Exportable
    Export-KrCertificate -Certificate $cert `
        -FilePath 'devcert' -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}

if (-not (Test-KrCertificate -Certificate $cert )) {
    Write-Error 'Certificate validation failed. Ensure the certificate is valid and not self-signed.'
    exit 1
}

# Example usage:
Set-KrServerOptions -DenyServerHeader

Set-KrServerLimit -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
# Configure the listener (adjust port, cert path, and password)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $cert -Protocols Http1AndHttp2AndHttp3
#Add-KrEndpoint -Port $Port -IPAddress $IPAddress

Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @('text/plain', 'text/html', 'application/json', 'application/xml', 'application/x-www-form-urlencoded')

Add-KrFaviconMiddleware

# Authentication Schemes Names definitions
$BasicPowershellScheme = 'PowershellBasic'
$BasicCSharpScheme = 'CSharpBasic'
$BasicVBNetScheme = 'VBNetBasic'
$CookieScheme = 'Cookies'
$JwtScheme = 'Bearer'
$ApiKeySimple = 'ApiKeySimple'
$ApiKeyPowerShell = 'ApiKeyPowerShell'
$ApiKeyCSharp = 'ApiKeyCSharp'
$ApiKeyVBNet = 'ApiKeyVBNet'
$issuer = 'KestrunApi'
$audience = 'KestrunClients'

$incremental = [System.Collections.Concurrent.ConcurrentDictionary[string, int]]::New([System.StringComparer]::OrdinalIgnoreCase)
$incremental['Count'] = 0

# Claim Policies
$claimConfig = New-KrClaimPolicy |
    Add-KrClaimPolicy -PolicyName 'CanCreate' -ClaimType 'can_create' -AllowedValues 'true' |
    Add-KrClaimPolicy -PolicyName 'CanDelete' -ClaimType 'can_delete' -AllowedValues 'true' |
    Add-KrClaimPolicy -PolicyName 'CanRead' -ClaimType 'can_read' -AllowedValues 'true' |
    Add-KrClaimPolicy -PolicyName 'CanWrite' -ClaimType 'can_write' -AllowedValues 'true' |
    Add-KrClaimPolicy -PolicyName 'Admin' -UserClaimType Role -AllowedValues 'admin' |
    Build-KrClaimPolicy

# ── BASIC AUTHENTICATION ────────────────────────────────────────────────
Add-KrBasicAuthentication -Name $BasicPowershellScheme -Realm 'Power-Kestrun' -AllowInsecureHttp -ScriptBlock {
    param($Username, $Password)
    Write-KrLog -Level Information -Message 'Basic Authentication: User {user} is trying to authenticate.' -Values $Username
    if ($Username -eq 'admin' -and $Password -eq 'password') {
        $true
    } else {
        $false
    }
} -IssueClaimsScriptBlock {
    param([string]$Identity)
    if ($Identity -eq 'admin') {
        # Return claims for the admin user
        return (Add-KrUserClaim -UserClaimType Role -Value 'admin' |
                Add-KrUserClaim -ClaimType 'can_read' -Value 'true' |
                Add-KrUserClaim -ClaimType 'can_write' -Value 'true' |
                Add-KrUserClaim -ClaimType 'can_delete' -Value 'false' |
                Add-KrUserClaim -ClaimType 'can_create' -Value 'true')
    } else {
        return [System.Security.Claims.Claim[]]@()
    }
} -Logger $logger -ClaimPolicyConfig $claimConfig



Add-KrBasicAuthentication -Name $BasicCSharpScheme -Realm 'CSharp-Kestrun' -AllowInsecureHttp -Code @'
   // Log.Information("Validating credentials for {Username}", username);
    return username == "admin" && password == "password";
'@ -CodeLanguage CSharp


Add-KrBasicAuthentication -Name $BasicVBNetScheme -Realm 'VBNet-Kestrun' -AllowInsecureHttp -Code @'
    ' Log.Information("Validating credentials for {Username}", username)
    Return username = "admin" AndAlso password = "password"
'@ -CodeLanguage VBNet -IssueClaimsCode @'
    If Identity = "admin" Then          ' (VB is case-insensitive, but keep it consistent)
        Return New List(Of System.Security.Claims.Claim) From {
            New System.Security.Claims.Claim("can_write", "true"),
            New System.Security.Claims.Claim("can_create", "true")
        }
    End If

    ' everyone else gets no extra claims
    Return Enumerable.Empty(Of System.Security.Claims.Claim)()
'@ -IssueClaimsCodeLanguage VBNet -ClaimPolicyConfig $claimConfig -Logger $logger

# ── WINDOWS AUTHENTICATION ────────────────────────────────────────────
Add-KrWindowsAuthentication



Add-KrApiKeyAuthentication -Name $ApiKeySimple -AllowInsecureHttp -HeaderName 'X-Api-Key' -ExpectedKey 'my-secret-api-key'



Add-KrApiKeyAuthentication -Name $ApiKeyPowerShell -AllowInsecureHttp -HeaderName 'X-Api-Key' -ScriptBlock {
    param(
        [string]$ProvidedKey
    )
    if ($ProvidedKey -eq 'my-secret-api-key') {
        return $true
    } else {
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


Add-KrApiKeyAuthentication -Name $ApiKeyCSharp -AllowInsecureHttp -HeaderName 'X-Api-Key' -Code @'
    return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key");
    // or use a simple string comparison:
    //return providedKey == "my-secret-api-key";
'@

Add-KrApiKeyAuthentication -Name $ApiKeyVBNet -AllowInsecureHttp -HeaderName 'X-Api-Key' -Code @'
    Return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key")
    ' or use a simple string comparison:
    ' Return providedKey = "my-secret-api-key"
'@ -CodeLanguage VBNet


# ── JWT AUTHENTICATION ────────────────────────────────────────────────

$JwtKeyHex = '6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0'
$JwtTokenBuilder = New-KrJWTBuilder |
    Add-KrJWTIssuer -Issuer $issuer |
    Add-KrJWTAudience -Audience $audience |
    #| Set-JwtSubject   -Subject  'admin' `
    Protect-KrJWT -HexadecimalKey $JwtKeyHex -Algorithm HS256

$result = Build-KrJWT -Builder $JwtTokenBuilder
#$jwt     = Get-JwtToken -Result $result
$jwtOptions = $result | Get-KrJWTValidationParameter

Add-KrJWTBearerAuthentication -Name $JwtScheme -ValidationParameter $jwtOptions -ClaimPolicy $claimConfig


$cookie = [Microsoft.AspNetCore.Http.CookieBuilder]::new()

$cookie.Name = 'KestrunAuth'
$cookie.HttpOnly = $true
$cookie.SecurePolicy = [Microsoft.AspNetCore.Http.CookieSecurePolicy]::Always
$cookie.SameSite = [Microsoft.AspNetCore.Http.SameSiteMode]::Strict

# ---- Cookies Authentication ----
Add-KrCookiesAuthentication -Name $CookieScheme -LoginPath '/cookies/login' -LogoutPath '/cookies/logout' `
    -AccessDeniedPath '/cookies/access-denied' -SlidingExpiration -ExpireTimeSpan (New-TimeSpan -Minutes 60) `
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

Add-KrRouteGroup -Prefix '/secure/ps' -AuthorizationSchema $BasicPowershellScheme {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType 'text/plain'
    }

    Add-KrRouteGroup -Prefix '/policy' {
        Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
                HttpVerbs = 'Get'
                ScriptCode = @{
                    Code = {
                        $user = $Context.User.Identity.Name
                        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_read' permission." -ContentType 'text/plain'
                    }
                    Language = 'PowerShell'
                }
                RequirePolicies = @('CanRead')
            })


        Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
                HttpVerbs = 'Delete'
                ScriptCode = @{
                    Code = {
                        $user = $Context.User.Identity.Name
                        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_delete' permission." -ContentType 'text/plain'
                    }
                    Language = 'PowerShell'
                }
                RequirePolicies = @('CanDelete')
            })

        Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
                HttpVerbs = 'Put'
                ScriptCode = @{
                    Code = {
                        $user = $Context.User.Identity.Name
                        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_write' permission." -ContentType 'text/plain'
                    }
                    Language = 'PowerShell'
                }
                RequirePolicies = @('CanWrite')
            })

        Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
                HttpVerbs = 'Post'
                ScriptCode = @{
                    Code = {
                        $user = $Context.User.Identity.Name
                        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_create' permission." -ContentType 'text/plain'
                    }
                    Language = 'PowerShell'
                }
                RequirePolicies = @('CanCreate')
            })
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/secure/cs/hello' -AuthorizationSchema $BasicCSharpScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType 'text/plain'
}

Add-KrMapRoute -Verbs Get -Pattern '/secure/vb/hello' -AuthorizationSchema $BasicVBNetScheme -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code." -ContentType 'text/plain'
}



Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
        Pattern = '/secure/vb/policy'
        HttpVerbs = 'Get'
        ScriptCode = @{
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_read' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
        }
        RequirePolicies = @('CanRead')
        RequireSchemes = @($BasicVBNetScheme)
    })


Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
        Pattern = '/secure/vb/policy'
        HttpVerbs = 'Delete'
        ScriptCode = @{
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_delete' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
        }
        RequirePolicies = @('CanDelete')
        RequireSchemes = @($BasicVBNetScheme)
    })

Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
        Pattern = '/secure/vb/policy'
        HttpVerbs = 'Put'
        ScriptCode = @{
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_write' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
        }
        RequirePolicies = @('CanWrite')
        RequireSchemes = @($BasicVBNetScheme)
    })

Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
        Pattern = '/secure/vb/policy'
        HttpVerbs = 'Post'
        ScriptCode = @{
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_create' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
        }
        RequirePolicies = @('CanCreate')
        RequireSchemes = @($BasicVBNetScheme)
    })


# KESTRUN KEY MATCHING AUTHENTICATION ROUTES
Add-KrRouteGroup -Prefix '/secure/key' {

    Add-KrMapRoute -Verbs Get -Pattern '/simple/hello' -AuthorizationSchema $ApiKeySimple -ScriptBlock {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated using simple key matching." -ContentType 'text/plain'
    }

    Add-KrMapRoute -Verbs Get -Pattern '/ps/hello' -AuthorizationSchema $ApiKeyPowerShell -ScriptBlock {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code." -ContentType 'text/plain'
    }

    Add-KrMapRoute -Verbs Get -Pattern '/cs/hello' -AuthorizationSchema $ApiKeyCSharp -ScriptBlock {

        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching C# Code." -ContentType 'text/plain'
    }
    Add-KrMapRoute -Verbs Get -Pattern '/Vb/hello' -AuthorizationSchema $ApiKeyVBNet -ScriptBlock {

        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching VB.NET Code." -ContentType 'text/plain'
    }

    # KESTRUN KEY MATCHING AUTHENTICATION ROUTES
    Add-KrRouteGroup -Prefix '/ps/policy' -AuthorizationSchema $ApiKeyPowerShell {
        Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_read' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanRead'

        Add-KrMapRoute -Verbs Put -Pattern '/' -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_write' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanWrite'

        Add-KrMapRoute -Verbs Post -Pattern '/' -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_create' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanCreate'

        Add-KrMapRoute -Verbs Delete -Pattern '/' -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_delete' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanDelete'
    }
}

# KESTRUN JWT AUTHENTICATION ROUTES
Add-KrRouteGroup -Prefix '/secure/jwt' -AuthorizationSchema $JwtScheme {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        Expand-KrObject $Context
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by JWT Bearer Token." -ContentType 'text/plain'
    }
    Add-KrRouteGroup -Prefix '/policy' {
        Add-KrMapRoute -Verbs Get -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_read' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanRead'

        Add-KrMapRoute -Verbs Put -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_write' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanWrite'

        Add-KrMapRoute -Verbs Post -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_create' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanCreate'

        Add-KrMapRoute -Verbs Delete -ScriptBlock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_delete' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanDelete'
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/token/renew' -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name

    Write-KrLog -Level Information -Message 'Generating JWT token for user {0}' -Values $user
    Write-Output "JwtTokenBuilder Type : $($JwtTokenBuilder.GetType().FullName)"
    $accessToken = $JwtTokenBuilder | Update-KrJWT -FromContext
    Write-KrJsonResponse -InputObject @{
        access_token = $accessToken
        token_type = 'Bearer'
        expires_in = $build.Expires
    } -ContentType 'application/json'
}


Add-KrMapRoute -Verbs Get -Pattern '/token/new' -AuthorizationSchema $BasicPowershellScheme -ScriptBlock {
    $user = $Context.User.Identity.Name

    Write-KrLog -Level Information -Message 'Regenerating JWT token for user {0}' -Values $user
    Write-KrLog -Level Information -Message 'JWT Token Builder: {0}' -Values $JwtTokenBuilder
    if (-not $JwtTokenBuilder) {
        Write-KrErrorResponse -Message 'JWT Token Builder is not initialized.' -StatusCode 500
        return
    }
    Write-Output "JwtTokenBuilder Type : $($JwtTokenBuilder.GetType().FullName)"
    Write-Output "Issuer : $($JwtTokenBuilder.Issuer)"
    Write-Output "Audience : $($JwtTokenBuilder.Audience)"
    Write-Output "Algorithm: $($JwtTokenBuilder.Algorithm)"

    $build = Copy-KrJWTTokenBuilder -Builder $JwtTokenBuilder |
        Add-KrJWTSubject -Subject $user |
        Add-KrJWTClaim -UserClaimType Name -Value $user |
        Add-KrJWTClaim -UserClaimType Role -Value 'admin' |
        Add-KrJWTClaim -ClaimType 'can_read' -Value 'true' | Build-KrJWT
    $accessToken = $build | Get-KrJWTToken
    Write-KrJsonResponse -InputObject @{
        access_token = $accessToken
        token_type = 'Bearer'
        expires_in = $build.Expires
    } -ContentType 'application/json'
} -Arguments @{'JwtTokenBuilder' = $JwtTokenBuilder }

<#
********************************************
    Cookie authentication routes
*********************************************
#>
Add-KrMapRoute -Verbs Get -Pattern '/cookies/login' -ScriptBlock {
    Write-KrTextResponse -InputObject @'
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
'@ -ContentType 'text/html'
}

Add-KrMapRoute -Verbs Post -Pattern '/cookies/login' -ScriptBlock {
    $form = $Context.Request.Form
    if ($null -eq $form) {
        Write-KrJsonResponse -InputObject @{ success = $false; error = 'Form data missing' } -ContentType 'application/json'
        return
    }
    $username = $form['username']
    $password = $form['password']

    if ($username -eq 'admin' -and $password -eq 'secret') {
        $claims = (Add-KrUserClaim -UserClaimType Name -Value $username |
                Add-KrUserClaim -UserClaimType Role -Value 'admin' |
                Add-KrUserClaim -ClaimType 'can_read' -Value 'true' |
                Add-KrUserClaim -ClaimType 'can_write' -Value 'true' |
                Add-KrUserClaim -ClaimType 'can_create' -Value 'true')
        $principal = Invoke-KrCookieSignIn -Scheme 'Cookies' -Claims $claims -PassThru
        Write-KrLog -Level Information -Message 'User {user} signed in with Cookies authentication.' -Values $username
        Expand-KrObject -InputObject $principal -Label 'Principal'
        Write-KrJsonResponse -InputObject @{ success = $true; message = 'Login successful' }
    } else {
        Write-KrJsonResponse -InputObject @{ success = $false; message = 'Invalid credentials.' } -StatusCode 401
    }
}


Add-KrMapRoute -Verbs Get -Pattern '/cookies/logout' -ScriptBlock {

    [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignOutAsync($Context.HttpContext, 'Cookies').Wait()
    Write-KrRedirectResponse -Url '/cookies/login'
} -AuthorizationSchema $CookieScheme


Add-KrRouteGroup -Prefix '/secure/cookies' -AuthorizationSchema $CookieScheme {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies Authentication." -ContentType 'text/plain'
    }

    Add-KrMapRoute -Verbs Get -Pattern '/map' -ScriptBlock {
        $incremental['Count'] = $incremental['Count'] + 1
        @{
            'BasicPowershellScheme' = $BasicPowershellScheme
            'BasicCSharpScheme' = $BasicCSharpScheme
            'BasicVBNetScheme' = $BasicVBNetScheme
            'CookieScheme' = $CookieScheme
            'JwtScheme' = $JwtScheme
            'ApiKeySimple' = $ApiKeySimple
            'ApiKeyPowerShell' = $ApiKeyPowerShell
            'ApiKeyCSharp' = $ApiKeyCSharp
            'ApiKeyVBNet' = $ApiKeyVBNet
            'Issuer' = $issuer
            'Audience' = $audience
            'ClaimConfig' = $claimConfig
            'Incremental' = $incremental
        } | Write-KrJsonResponse -ContentType 'application/json'
    }

    Add-KrRouteGroup -Prefix '/policy' {
        Add-KrMapRoute -Verbs Get {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_read' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanRead'

        Add-KrMapRoute -Verbs Put {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_write' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanWrite'

        Add-KrMapRoute -Verbs Post {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_create' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanCreate'

        Add-KrMapRoute -Verbs Delete {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies checker because you have the 'can_delete' permission." -ContentType 'text/plain'
        } -AuthorizationPolicy 'CanDelete'
    }
}

<#
********************************************
# Start the server asynchronously
********************************************
#>
Start-KrServer -Server $server

