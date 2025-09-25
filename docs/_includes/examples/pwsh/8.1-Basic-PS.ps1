<#
    Sample: Basic Authentication (PowerShell)
    Purpose: Demonstrates creating a Basic authentication scheme using a PowerShell script block for credential validation.
    File:    8.1-Basic-PS.ps1
    Notes:   Plain-text password comparison for tutorial purposes only. Use secure storage in production.
#>

# 1. (Optional) Logging pipeline
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server host
New-KrServer -Name 'Auth Basic PS'

# 3. Add HTTP listener (loopback 5000)
Add-KrEndpoint -Port 5000 -IPAddress ([IPAddress]::Loopback)

# 4. Enable PowerShell runtime for route script blocks
Add-KrPowerShellRuntime

# 5. Define Basic auth scheme with inline validation script
Add-KrBasicAuthentication -Name 'PowershellBasic' -Realm 'Demo' -AllowInsecureHttp -ScriptBlock {
    param($Username, $Password)
    $Username -eq 'admin' -and $Password -eq 'password'
}

# 6. Finalize configuration (build internal pipeline)
Enable-KrConfiguration

# 7. Map secured route group using the scheme
Add-KrRouteGroup -Prefix '/secure/ps' -AuthorizationSchema 'PowershellBasic' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        Write-KrTextResponse -InputObject "Hello $( $Context.User.Identity.Name )" -ContentType 'text/plain'
    }
}

# 8. Start server (Ctrl+C to stop)
Start-KrServer -CloseLogsOnExit

