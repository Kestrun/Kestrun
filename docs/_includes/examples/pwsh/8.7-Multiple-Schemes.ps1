<#
        Sample: Multiple Authentication Schemes
        Purpose: Combine Basic, API Key, and JWT Bearer schemes in one server.
        File:    8.7-Multiple-Schemes.ps1
        Notes:   Shows grouping routes and applying distinct schemes per endpoint.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth Multi'

# 3. Listener
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Basic auth scheme
Add-KrBasicAuthentication -Name 'BasicPS' -Realm 'Multi' -AllowInsecureHttp -ScriptBlock {
    param($Username, $Password) # Plain text for tutorial simplicity
    $Username -eq 'admin' -and $Password -eq 'password'
}

# 6. API key scheme
Add-KrApiKeyAuthentication -Name 'KeySimple' -AllowInsecureHttp -HeaderName 'X-Api-Key' -ExpectedKey 'my-secret-api-key'

# 7. JWT bearer scheme setup
$builder = New-KrJWTBuilder |
    Add-KrJWTIssuer -Issuer 'KestrunApi' |
    Add-KrJWTAudience -Audience 'KestrunClients' |
    Protect-KrJWT -HexadecimalKey '6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0' -Algorithm HS256
$res = Build-KrJWT -Builder $builder
$validation = $res | Get-KrJWTValidationParameter
Add-KrJWTBearerAuthentication -Name 'Bearer' -ValidationParameter $validation

# 8. Finalize configuration
Enable-KrConfiguration

# 9. Map routes with different schemes
Add-KrRouteGroup -Prefix '/secure' {
    Add-KrMapRoute -Verbs Get -Pattern '/basic' -AuthorizationSchema 'BasicPS' -ScriptBlock { Write-KrTextResponse 'Basic OK' }
    Add-KrMapRoute -Verbs Get -Pattern '/key' -AuthorizationSchema 'KeySimple' -ScriptBlock { Write-KrTextResponse 'Key OK' }
    Add-KrMapRoute -Verbs Get -Pattern '/jwt' -AuthorizationSchema 'Bearer' -ScriptBlock { Write-KrTextResponse 'JWT OK' }
}

# 10. Start server
Start-KrServer

