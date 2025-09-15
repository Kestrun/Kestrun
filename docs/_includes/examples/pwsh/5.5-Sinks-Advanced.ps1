<#
    Sample demonstrating advanced sinks: HTTP, EventLog, and Syslog.
    Notes:
      - HTTP requires a reachable endpoint; left commented by default.
      - EventLog typically needs admin rights and source setup; commented.
      - Syslog requires a server; configure host/port as needed.
    The script still runs standalone using console + file sinks.
    FileName: 5.5-Sinks-Advanced.ps1
#>

$base = New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Information |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path './logs/advanced-sinks.log' -RollingInterval Hour

# Optional: HTTP sink (uncomment and set URL)
# $base = $base | Add-KrSinkHttp -RequestUri 'http://localhost:9000/api/events' -BatchPostingLimit 50

# Optional: EventLog sink (requires admin; source/log must exist or be managed)
# $base = $base | Add-KrSinkEventLog -Source 'Kestrun' -LogName 'Application' -ManageEventSource

# Syslog example (choose one)
# $base = $base | Add-KrSinkSyslogUdp -Hostname '127.0.0.1' -Port 514 -AppName 'KestrunSample'
# $base = $base | Add-KrSinkSyslogTcp -Hostname '127.0.0.1' -Port 514 -AppName 'KestrunSample' -Format RFC3164
# Local syslog (Linux/macOS)
$base = $base | Add-KrSinkSyslogLocal -AppName 'KestrunSample'

$logger = $base | Register-KrLogger -Name 'advanced' -PassThru

New-KrServer -Name "Advanced Sinks"
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Path "/log" -ScriptBlock {
    Write-KrLog -Logger $logger -Level Information -Message "Advanced sinks example"
    Write-KrTextResponse -InputObject "ok" -StatusCode 200
}

# Start the server
Start-KrServer

# Clean up and close the logger when the server stops
Close-KrLogger -Logger $logger
