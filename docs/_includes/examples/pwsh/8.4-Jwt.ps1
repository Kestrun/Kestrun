<#
    Sample: JWT Token Issuance & Validation
    Purpose: Show issuing a JWT after Basic auth and protecting routes with a bearer scheme.
    File:    8.4-Jwt.ps1
    Notes:   Uses symmetric HMAC key; store securely (e.g., KeyVault, env var) in production.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth JWT'

# 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Initial Basic scheme for token issuance
Add-KrBasicAuthentication -Name 'BasicInit' -Realm 'Init' -AllowInsecureHttp -ScriptBlock { param($Username, $Password) $Username -eq 'admin' -and $Password -eq 'password' }

# 6. Build JWT configuration
$jwtBuilder = New-KrJWTBuilder |
    Add-KrJWTIssuer -Issuer 'KestrunApi' |
    Add-KrJWTAudience -Audience 'KestrunClients' |
    Protect-KrJWT -HexadecimalKey '6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0' -Algorithm HS256
$result = Build-KrJWT -Builder $jwtBuilder
$validation = $result | Get-KrJWTValidationParameter

# 7. Register bearer scheme
Add-KrJWTBearerAuthentication -Name 'Bearer' -ValidationParameter $validation

# 8. Finalize configuration
Enable-KrConfiguration

# 9. Route: issue token (requires Basic)
Add-KrMapRoute -Verbs Get -Pattern '/token/new' -AuthorizationSchema 'BasicInit' -ScriptBlock {
    $user = $Context.User.Identity.Name
    Write-KrLog -Level Information -Message 'Generating JWT token for user {User}' -Values $user
    Write-KrLog -Level Information -Message "Issuer : {Issuer} " -Values $JwtTokenBuilder.Issuer
    Write-KrLog -Level Information -Message "Audience : {Audience} " -Values $JwtTokenBuilder.Audience
    Write-KrLog -Level Information -Message "Algorithm: {Algorithm} " -Values $JwtTokenBuilder.Algorithm

    $build = Copy-KrJWTTokenBuilder -Builder $jwtBuilder |
        Add-KrJWTSubject -Subject $user |
        Add-KrJWTClaim -UserClaimType Name -Value $user |
        Add-KrJWTClaim -UserClaimType Role -Value 'admin' |
        Build-KrJWT
    $token = $build | Get-KrJWTToken
    Write-KrJsonResponse @{ access_token = $token; expires = $build.Expires }
}

Add-KrMapRoute -Verbs Get -Pattern '/token/renew' -AuthorizationSchema $JwtScheme -ScriptBlock {
    $user = $Context.User.Identity.Name

    Write-KrLog -Level Information -Message 'Generating JWT token for user {0}' -Values $user
    $accessToken = $jwtBuilder | Update-KrJWT -FromContext
    Write-KrJsonResponse -InputObject @{
        access_token = $accessToken
        token_type = 'Bearer'
        expires_in = $build.Expires
    } -ContentType 'application/json'
}

# 10. Route: protected with bearer token
Add-KrMapRoute -Verbs Get -Pattern '/secure/jwt/hello' -AuthorizationSchema 'Bearer' -ScriptBlock {
    Write-KrTextResponse -InputObject "JWT Hello $( $Context.User.Identity.Name )"
}

# 11. Start server
Start-KrServer -CloseLogsOnExit

