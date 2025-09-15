<#
    Sample demonstrating multiple named loggers with different minimum levels.
    'app' logger: Minimum Information, writes to console + app.log
    'audit' logger: Minimum Debug, writes to audit.log
    FileName: 5.2-Multiple-Loggers-Levels.ps1
#>

$appLogger = New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Information |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path '.\logs\app.log' -RollingInterval Hour |
    Register-KrLogger -Name 'app' -PassThru

$auditLogger = New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\audit.log' -RollingInterval Hour |
    Register-KrLogger -Name 'audit' -PassThru

# Create server and listener
New-KrServer -Name "Multiple Loggers Server"
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# PowerShell runtime is required for script block routes
Add-KrPowerShellRuntime

Write-KrLog -Logger $appLogger -Level Information -Message "Server created"
Write-KrLog -Logger $auditLogger -Level Debug -Message "Audit logger active"

Enable-KrConfiguration

# Routes
Add-KrMapRoute -Verbs Get -Path "/info" -ScriptBlock {
    Write-KrLog -Logger $appLogger -Level Information -Message "Info route handled"
    Write-KrTextResponse -InputObject "info" -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Path "/debug" -ScriptBlock {
    # This Debug will be filtered out by 'app' (min Information)
    Write-KrLog -LoggerName 'app' -Level Debug -Message "App debug (filtered)"
    # This Debug will be written by 'audit' (min Debug)
    Write-KrLog -Logger $auditLogger -Level Debug -Message "Audit debug (written)"
    Write-KrTextResponse -InputObject "debug" -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Path "/audit" -ScriptBlock {
    Write-KrLog -Logger $auditLogger -Level Information -Message "Audit event recorded"
    Write-KrTextResponse -InputObject "audit" -StatusCode 200
}

# Start the server
Start-KrServer

# Clean up and close all the loggers when the server stops
Close-KrLogger

