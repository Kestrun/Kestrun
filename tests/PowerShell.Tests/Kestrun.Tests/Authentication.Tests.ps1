
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param()
<#
    .SYNOPSIS
        Kestrun PowerShell Example: Multi Routes
    .DESCRIPTION
        This script demonstrates how to define multiple routes in Kestrun, a PowerShell web server framework.
    .EXAMPLE
        .\Authentication.ps1
        This example shows how to set up a Kestrun server with multiple authentication methods,
        including Basic Authentication, API Key Authentication, and JWT Bearer Token Authentication.
        It also demonstrates how to secure routes using these authentication methods.

    .NOTES
        This script is for demonstration purposes only and may require modifications
        to work in your environment.
#>
BeforeAll {
    try {
        # Get the path of the current script
        # This allows the script to be run from any location
        $TestPath = './tests/PowerShell.Tests/Kestrun.Tests'
        # Get the parent directory of the examples path
        # This is useful for locating the Kestrun module
        $kestrunPath = Split-Path -Parent -Path (Split-Path -Parent -Path (Split-Path -Parent -Path $TestPath))
        # Construct the path to the Kestrun module
        # This assumes the Kestrun module is located in the src/PowerShell/Kestr
        $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"
        # Import the Kestrun module from the source path if it exists, otherwise from installed modules
        if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
            Import-Module $kestrunModulePath -Force -ErrorAction Stop
        } else {
            Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
        }
    } catch {
        Write-Error "Failed to import Kestrun module: $_"
        Write-Error 'Ensure the Kestrun module is installed or the path is correct.'
        exit 1
    }
    # Load the helper functions
    # . "$PWD$TestPath/helper.ps1"
    $Script:Url = 'https://localhost:5001'
    $logger = New-KrLogger |
        Set-KrMinimumLevel -Value Debug |
        Add-KrSinkFile -Path '.\logs\Authentication.log' -RollingInterval Hour |
        Register-KrLogger -Name 'DefaultLogger' -PassThru -SetAsDefault

    New-KrServer -Name 'Kestrun Authentication'

    $cert = New-KrSelfSignedCertificate -DnsName 'localhost'
    if (-not (Test-KsCertificate -Certificate $cert )) {
        Write-Error 'Certificate validation failed. Ensure the certificate is valid and not self-signed.'
        exit 1
    }
    #>
    # Example usage:
    Set-KrServerOption -DenyServerHeader

    Set-KrServerLimit -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
    # Configure the listener (adjust port, cert path, and password)
    Add-KrListener -Port 5001 -IPAddress ([IPAddress]::Loopback) -X509Certificate $cert -Protocols Http1AndHttp2AndHttp3
    Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

    Add-KrResponseCompression -EnableForHttps -MimeTypes @('text/plain', 'text/html', 'application/json', 'application/xml', 'application/x-www-form-urlencoded')
    Add-KrPowerShellRuntime
    Add-KrFavicon

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

    Add-KrBasicAuthentication -Name $BasicPowershellScheme -Realm 'Power-Kestrun' -AllowInsecureHttp -Scriptblock {
        param([string]$Username, [string]$Password)
        Write-KrInformationLog -Message 'Basic Authentication: User {0} is trying to authenticate.' -Values $Username
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



    Add-KrApiKeyAuthentication -Name $ApiKeyPowerShell -AllowInsecureHttp -HeaderName 'X-Api-Key' -Scriptblock {
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
'@ -Logger $logger -CodeLanguage CSharp

    Add-KrApiKeyAuthentication -Name $ApiKeyVBNet -AllowInsecureHttp -HeaderName 'X-Api-Key' -Code @'
    Return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key")
    ' or use a simple string comparison:
    ' Return providedKey = "my-secret-api-key"
'@ -CodeLanguage VBNet -Logger $logger


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
    Add-KrMapRoute -Verbs Get -Pattern '/secure/ps/hello' -AuthorizationSchema $BasicPowershellScheme -Scriptblock {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType 'text/plain'
    }

    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/ps/policy'
            HttpVerbs = 'Get'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_read' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanRead')
            RequireSchemes = @($BasicPowershellScheme)
        })


    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/ps/policy'
            HttpVerbs = 'Delete'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_delete' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanDelete')
            RequireSchemes = @($BasicPowershellScheme)
        })

    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/ps/policy'
            HttpVerbs = 'Put'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_write' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanWrite')
            RequireSchemes = @($BasicPowershellScheme)
        })

    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/ps/policy'
            HttpVerbs = 'Post'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code because you have the 'can_create' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanCreate')
            RequireSchemes = @($BasicPowershellScheme)
        })


    Add-KrMapRoute -Verbs Get -Pattern '/secure/cs/hello' -AuthorizationSchema $BasicCSharpScheme -Scriptblock {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType 'text/plain'
    }

    Add-KrMapRoute -Verbs Get -Pattern '/secure/vb/hello' -AuthorizationSchema $BasicVBNetScheme -Scriptblock {
        $user = $Context.User.Identity.Name
        Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code." -ContentType 'text/plain'
    }



    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/vb/policy'
            HttpVerbs = 'Get'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_read' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanRead')
            RequireSchemes = @($BasicVBNetScheme)
        })


    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/vb/policy'
            HttpVerbs = 'Delete'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_delete' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanDelete')
            RequireSchemes = @($BasicVBNetScheme)
        })

    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/vb/policy'
            HttpVerbs = 'Put'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_write' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanWrite')
            RequireSchemes = @($BasicVBNetScheme)
        })

    Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
            Pattern = '/secure/vb/policy'
            HttpVerbs = 'Post'
            Code = {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code because you have the 'can_create' permission." -ContentType 'text/plain'
            }
            Language = 'PowerShell'
            RequirePolicies = @('CanCreate')
            RequireSchemes = @($BasicVBNetScheme)
        })

    # KESTRUN KEY MATCHING AUTHENTICATION ROUTES
    Add-KrRouteGroup -Prefix '/secure/key' {

        Add-KrMapRoute -Verbs Get -Pattern '/simple/hello' -AuthorizationSchema $ApiKeySimple -Scriptblock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated using simple key matching." -ContentType 'text/plain'
        }

        Add-KrMapRoute -Verbs Get -Pattern '/ps/hello' -AuthorizationSchema $ApiKeyPowerShell -Scriptblock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code." -ContentType 'text/plain'
        }

        Add-KrMapRoute -Verbs Get -Pattern '/cs/hello' -AuthorizationSchema $ApiKeyCSharp -Scriptblock {

            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching C# Code." -ContentType 'text/plain'
        }
        Add-KrMapRoute -Verbs Get -Pattern '/Vb/hello' -AuthorizationSchema $ApiKeyVBNet -Scriptblock {

            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching VB.NET Code." -ContentType 'text/plain'
        }

        # KESTRUN KEY MATCHING AUTHENTICATION ROUTES
        Add-KrRouteGroup -Prefix '/ps/policy' -AuthorizationSchema $ApiKeyPowerShell {
            Add-KrMapRoute -Verbs Get -Pattern '/' -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_read' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanRead'

            Add-KrMapRoute -Verbs Put -Pattern '/' -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_write' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanWrite'

            Add-KrMapRoute -Verbs Post -Pattern '/' -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_create' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanCreate'

            Add-KrMapRoute -Verbs Delete -Pattern '/' -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Key Matching PowerShell Code because you have the 'can_delete' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanDelete'
        }
    }


    # KESTRUN JWT AUTHENTICATION ROUTES
    Add-KrRouteGroup -Prefix '/secure/jwt' -AuthorizationSchema $JwtScheme {
        Add-KrMapRoute -Verbs Get -Pattern '/hello' -Scriptblock {
            Expand-KrObject $Context
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by JWT Bearer Token." -ContentType 'text/plain'
        }
        Add-KrRouteGroup -Prefix '/policy' {
            Add-KrMapRoute -Verbs Get -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_read' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanRead'

            Add-KrMapRoute -Verbs Put -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_write' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanWrite'

            Add-KrMapRoute -Verbs Post -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_create' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanCreate'

            Add-KrMapRoute -Verbs Delete -Scriptblock {
                $user = $Context.User.Identity.Name
                Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native JWT checker because you have the 'can_delete' permission." -ContentType 'text/plain'
            } -AuthorizationPolicy 'CanDelete'
        }
    }

    Add-KrMapRoute -Verbs Get -Pattern '/token/renew' -AuthorizationSchema $JwtScheme -Scriptblock {
        $user = $Context.User.Identity.Name

        Write-KrInformationLog -Message 'Generating JWT token for user {0}' -Values $user
        Write-Output "JwtTokenBuilder Type : $($JwtTokenBuilder.GetType().FullName)"
        $accessToken = $JwtTokenBuilder | Update-KrJWT -FromContext
        Write-KrJsonResponse -InputObject @{
            access_token = $accessToken
            token_type = 'Bearer'
            expires_in = $build.Expires
        } -ContentType 'application/json'
    }

    Add-KrMapRoute -Verbs Get -Pattern '/token/new' -AuthorizationSchema $BasicPowershellScheme -Scriptblock {
        $user = $Context.User.Identity.Name

        Write-KrInformationLog -Message 'Regenerating JWT token for user {0}' -Values $user
        Write-KrInformationLog -Message 'JWT Token Builder: {0}' -Values $JwtTokenBuilder
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
    Add-KrMapRoute -Verbs Get -Pattern '/cookies/login' -Scriptblock {
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

    Add-KrMapRoute -Verbs Post -Pattern '/cookies/login' -Scriptblock {
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
            #Expand-KrObject $claims -Label "User Claims"
            $identity = [System.Security.Claims.ClaimsIdentity]::new( $claims, 'Cookies')
            #    [System.Security.Claims.ClaimTypes]::Name,
            #   [System.Security.Claims.ClaimTypes]::Role)
            $principal = [System.Security.Claims.ClaimsPrincipal]::new($identity)
            [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignInAsync($Context.HttpContext, 'Cookies', $principal).GetAwaiter().GetResult()
            Write-KrJsonResponse -InputObject @{ success = $true; message = 'Login successful' }
        } else {
            Write-KrJsonResponse -InputObject @{ success = $false; message = 'Invalid credentials.' } -StatusCode 401
        }
    }


    Add-KrMapRoute -Verbs Get -Pattern '/cookies/logout' -Scriptblock {

        [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignOutAsync($Context.HttpContext, 'Cookies').Wait()
        Write-KrRedirectResponse -Location '/cookies/login'
    } -AuthorizationSchema $CookieScheme



    Add-KrRouteGroup -Prefix '/secure/cookies' -AuthorizationSchema $CookieScheme {
        Add-KrMapRoute -Verbs Get -Pattern '/hello' -Scriptblock {
            $user = $Context.User.Identity.Name
            Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Cookies Authentication." -ContentType 'text/plain'
        }

        Add-KrMapRoute -Verbs Get -Pattern '/map' -Scriptblock {
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
    Start-KrServer -Server $server -NoWait -Quiet
    #    Wait-ForWebServer -Url "$url" -TimeoutSeconds 30
    Start-Sleep -Seconds 2
}

AfterAll {
    Stop-KrServer -NoClearVariable -Quiet
}
Describe 'Kestrun Authentication' {

    Describe 'Basic Authentication' {
        BeforeAll {
            $creds = 'admin:password'
            $script:basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))
        }

        It 'ps/hello in PowerShell' {
            $result = Invoke-WebRequest -Uri "$url/secure/ps/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by PowerShell Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/hello in C#' {
            $result = Invoke-WebRequest -Uri "$url/secure/cs/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by C# Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanRead)' {
            $result = Invoke-WebRequest -Uri "$url/secure/ps/policy" -Method GET -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by PowerShell Code because you have the 'can_read' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$url/secure/ps/policy" -Method DELETE -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'ps/policy (CanCreate)' {
            $result = Invoke-WebRequest -Uri "$url/secure/ps/policy" -Method POST -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by PowerShell Code because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanUpdate)' {
            $result = Invoke-WebRequest -Uri "$url/secure/ps/policy" -Method PUT -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by PowerShell Code because you have the 'can_write' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$url/secure/ps/policy" -Method PATCH -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*405*'
        }


        It 'vb/hello in VB.Net' {
            $result = Invoke-WebRequest -Uri "$url/secure/vb/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by VB.Net Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'vb/policy (CanRead)' {
            { Invoke-WebRequest -Uri "$url/secure/vb/policy" -Method GET -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'vb/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$url/secure/vb/policy" -Method DELETE -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'vb/policy (CanCreate)' {
            $result = Invoke-WebRequest -Uri "$url/secure/vb/policy" -Method POST -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by VB.Net Code because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'vb/policy (CanUpdate)' {
            $result = Invoke-WebRequest -Uri "$url/secure/vb/policy" -Method PUT -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by VB.Net Code because you have the 'can_write' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'vb/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$url/secure/vb/policy" -Method PATCH -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*405*'
        }
    }

    Describe 'Key Authentication' {

        It 'key authentication Hello Simple mode' {
            $result = Invoke-WebRequest -Uri "$url/secure/key/simple/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated using simple key matching.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'key authentication Hello in powershell' {
            $result = Invoke-WebRequest -Uri "$url/secure/key/ps/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated by Key Matching PowerShell Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'key authentication Hello in CSharp' {
            $result = Invoke-WebRequest -Uri "$url/secure/key/cs/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated by Key Matching C# Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'key authentication Hello in VB.Net' {
            $result = Invoke-WebRequest -Uri "$url/secure/key/vb/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated by Key Matching VB.Net Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }


        It 'key/policy (CanRead)' {
            { Invoke-WebRequest -Uri "$url/secure/key/ps/policy" -Method GET -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'key/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$url/secure/key/ps/policy" -Method DELETE -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } `
                    -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'key/policy (CanCreate)' {
            $result = Invoke-WebRequest -Uri "$url/secure/key/ps/policy" -Method Post -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, ApiKeyClient! You are authenticated by Key Matching PowerShell Code because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'key/policy (CanUpdate)' {
            { Invoke-WebRequest -Uri "$url/secure/key/ps/policy" -Method Put -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'key/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$url/secure/key/ps/policy" -Method PATCH -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*405*'
        }
    }

    Describe 'JWT Authentication' {

        BeforeAll {
            $creds = 'admin:password'
            $basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))
            $script:token = (Invoke-RestMethod -Uri "$url/token/new" -SkipCertificateCheck -Headers @{ Authorization = $script:basic }).access_token
        }

        It 'New Token' {
            $Script:token | Should -Not -BeNullOrEmpty
        }

        It 'Hello JWT' {
            $result = Invoke-WebRequest -Uri "$url/secure/jwt/hello" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by JWT Bearer Token.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'jwt/policy (CanRead)' {
            $result = Invoke-WebRequest -Uri "$url/secure/jwt/policy" -Method Get -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Native JWT checker because you have the 'can_read' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'jwt/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$url/secure/jwt/policy" -Method Delete -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'jwt/policy (CanCreate)' {
            { Invoke-WebRequest -Uri "$url/secure/jwt/policy" -Method Post -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'jwt/policy (CanUpdate)' {
            { Invoke-WebRequest -Uri "$url/secure/jwt/policy" -Method Put -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'jwt/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$url/secure/jwt/policy" -Method PATCH -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*405*'
        }

        It 'Renew Token' {
            $token2 = (Invoke-RestMethod -Uri "$url/token/renew" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }).access_token
            $token2 | Should -Not -BeNullOrEmpty
            $result = Invoke-WebRequest -Uri "$url/secure/jwt/hello" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token2" }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by JWT Bearer Token.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
    }
    Describe 'Cookies Authentication' {
        BeforeAll {
            $script:authSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
            $result = Invoke-WebRequest -Uri "$url/cookies/login" -SkipCertificateCheck -Method Post -Body @{ username = 'admin'; password = 'secret' } -SessionVariable authSession
            $result.StatusCode | Should -Be 200
        }
        AfterAll {
            $script:authSession = $null
        }
        It 'Can access secure cookies endpoint' {
            $result = Invoke-WebRequest -Uri "$url/secure/cookies/hello" -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by Cookies Authentication.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (GET)' {
            $result = Invoke-WebRequest -Uri "$url/secure/cookies/policy" -Method GET -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Cookies checker because you have the 'can_read' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (DELETE)' {
            { Invoke-WebRequest -Uri "$url/secure/cookies/policy" -Method DELETE -SkipCertificateCheck -WebSession $authSession -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*404*'
        }
        It 'Can access secure cookies policy (POST)' {
            $result = Invoke-WebRequest -Uri "$url/secure/cookies/policy" -Method POST -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Cookies checker because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (PUT)' {
            $result = Invoke-WebRequest -Uri "$url/secure/cookies/policy" -Method PUT -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Cookies checker because you have the 'can_write' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (PATCH)' {
            { Invoke-WebRequest -Uri "$url/secure/cookies/policy" -Method PATCH -SkipCertificateCheck -WebSession $authSession -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*405*'
        }
    }
}