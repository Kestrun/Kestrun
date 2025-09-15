<#
    Sample: JWT Token Issuance & Validation
    Purpose: Show issuing a JWT after Basic auth and protecting routes with a bearer scheme.
    File:    8.4-Jwt.ps1
    Notes:   Uses symmetric HMAC key; store securely (e.g., KeyVault, env var) in production.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth JWT'

# 3. Listener
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Initial Basic scheme for token issuance
Add-KrBasicAuthentication -Name 'BasicInit' -Realm 'Init' -AllowInsecureHttp -ScriptBlock { param($Username, $Password) $Username -eq 'admin' -and $Password -eq 'password' }

#$requiredClaims = Add-KrRequiredClaim -ClaimType Role -AllowedValues "Admin"
Add-KrAuthorizationPolicy -Name "AdminPolicy" -RequireRoles "Admin"
Add-KrAuthorizationPolicy -Name "UserPolicy" -RequireRoles "User"

# 6. Build JWT configuration
$builder = New-KrJWTBuilder |
    Add-KrJWTIssuer -Issuer 'KestrunApi' | Add-KrJWTClaim -ClaimType 'Role' -Value 'Admin' |
    Add-KrJWTAudience -Audience 'KestrunClients' | Add-KrJWTSubject -Subject "mySubject" | Limit-KrJWTValidity -Lifetime 60 |
    Protect-KrJWT -HexadecimalKey '6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0' -Algorithm HS256

$result = Build-KrJWT -Builder $builder
$validation = $result | Get-KrJWTValidationParameter

# 7. Register bearer scheme
Add-KrJWTBearerAuthentication -Name 'Bearer' -ValidationParameter $validation

# 8. Finalize configuration
Enable-KrConfiguration

# 9. Route: issue token (requires Basic)
Add-KrMapRoute -Verbs Get -Pattern '/token/new' -AuthorizationSchema 'BasicInit' -ScriptBlock {
    $b = Copy-KrJWTTokenBuilder -Builder $builder | Add-KrJWTClaim -ClaimType 'can_read' -Value 'true' | Build-KrJWT
    $token = $b | Get-KrJWTToken
    Write-KrJsonResponse @{ access_token = $token; expires = $b.Expires }
}

# 10. Route: protected with bearer token
Add-KrMapRoute -Verbs Get -Pattern '/secure/jwt/helloAdmin' -AuthorizationSchema 'Bearer' -AuthorizationPolicy "AdminPolicy" -ScriptBlock {
    Write-KrTextResponse -InputObject "JWT Hello Admin $( $Context.User.Identity.Name )"
}

Add-KrMapRoute -Verbs Get -Pattern '/secure/jwt/helloUser' -AuthorizationSchema 'Bearer' -AuthorizationPolicy "UserPolicy" -ScriptBlock {
    Write-KrTextResponse -InputObject "JWT Hello User $( $Context.User.Identity.Name )"
}
# 11. Start server
Start-KrServer

