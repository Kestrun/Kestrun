<#
    Sample: API Key Authentication Variants
    Purpose: Demonstrates fixed key, PowerShell script, and inline C# validation for API key auth.
    File:    8.3-Api-Key.ps1
    Notes:   Keys are static for illustration. Rotate & store securely in production.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth API Key'

# 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Fixed key scheme
Add-KrApiKeyAuthentication -Name 'ApiKeySimple' -AllowInsecureHttp -HeaderName 'X-Api-Key' -ExpectedKey 'my-secret-api-key'

# 6. Script-based validation
Add-KrApiKeyAuthentication -Name 'ApiKeyPS' -AllowInsecureHttp -HeaderName 'X-Api-Key' -ScriptBlock { param($ProvidedKey) $ProvidedKey -eq 'my-secret-api-key' }

# 7. C# code validation
Add-KrApiKeyAuthentication -Name 'ApiKeyCS' -AllowInsecureHttp -HeaderName 'X-Api-Key' -Code @'
    return providedKey == "my-secret-api-key";
'@

# 8. Finalize configuration
Enable-KrConfiguration

# 9. Group routes by scheme variant
Add-KrRouteGroup -Prefix '/secure/key' {
    Add-KrMapRoute -Verbs Get -Pattern '/simple/hello' -AuthorizationSchema 'ApiKeySimple' -ScriptBlock { Write-KrTextResponse 'Simple Key OK' }
    Add-KrMapRoute -Verbs Get -Pattern '/ps/hello' -AuthorizationSchema 'ApiKeyPS' -ScriptBlock { Write-KrTextResponse 'PS Key OK' }
    Add-KrMapRoute -Verbs Get -Pattern '/cs/hello' -AuthorizationSchema 'ApiKeyCS' -ScriptBlock { Write-KrTextResponse 'C# Key OK' }
}

# 10. Start server
Start-KrServer -CloseLogsOnExit
