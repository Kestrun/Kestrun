

try {
    # Get the path of the current script
    # This allows the script to be run from any location
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    # Determine the script path and Kestrun module path
    $powerShellExamplesPath = (Split-Path -Parent -Path $ScriptPath)
    # Determine the script path and Kestrun module path
    $examplesPath = (Split-Path -Parent -Path $powerShellExamplesPath)
    # Get the parent directory of the examples path
    # This is useful for locating the Kestrun module
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    # Construct the path to the Kestrun module
    # This assumes the Kestrun module is located in the src/PowerShell/Kestr
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"

    # Import Kestrun module (from source if present, otherwise the installed module)
    if (Test-Path $kestrunModulePath -PathType Leaf) {
        # Import the Kestrun module from the source path if it exists
        # This allows for development and testing of the module without needing to install it
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        # If the source module does not exist, import the installed Kestrun module
        # This is useful for running the script in a production environment where the module is installed
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    # If the import fails, output an error message and exit
    # This ensures that the script does not continue running if the module cannot be loaded
    Write-Error "Failed to import Kestrun module: $_"
    exit 1
}
# 1.  ─── Logging ──────────────────────────────────────────────



$logger = New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\scheduling.log' -RollingInterval Hour |
    Register-KrLogger -SetAsDefault -Name 'DefaultLogger' -PassThru

Set-KrSharedState -Name 'Visits' -Value @{Count = 0 }

# 2.  ─── Host & core services ─────────────────────────────────
New-KrServer -Name 'MyKestrunServer' -Logger $logger

# Listen on port 5000 (HTTP)
Add-KrEndpoint -Port 5000 -PassThru |
    # Add run-space runtime & scheduler (8 RS for jobs)
    Add-KrScheduling -MaxRunspaces 8 -PassThru |
    # Seed a global counter (Visits) — injected as $Visits in every runspace
    Enable-KrConfiguration -PassThru
# 3.  ─── Scheduled jobs ───────────────────────────────────────

# (A) pure-C# heartbeat every 10 s (through ScriptBlock)
Register-KrSchedule -Name Heartbeat -Interval '00:00:10' -RunImmediately -ScriptBlock {
    Write-KrLog -Level Information -Message '💓  Heartbeat (PowerShell) at {0:O}' -Values $([DateTimeOffset]::UtcNow)
}


Register-KrSchedule -Name 'HeartbeatCS' -Interval '00:00:15' -Language CSharp -Code @'
    // C# code runs inside the server process
    Serilog.Log.Information("💓  Heartbeat (C#) at {0:O}", DateTimeOffset.UtcNow);
'@

# (B) inline PS every minute
Register-KrSchedule -Name 'ps-inline' -Cron '0 * * * * *' -ScriptBlock {
    Write-Information "[$([DateTime]::UtcNow.ToString('o'))] 🌙  Inline PS job ran."
    Write-Information "Runspace Name: $([runspace]::DefaultRunspace.Name)"
    Write-Information "$($Visits['Count']) Visits so far."
}

# (C) script file nightly 03:00
Register-KrSchedule -Name 'nightly-clean' -Cron '0 0 3 * * *' `
    -ScriptPath 'Scripts\Cleanup.ps1' -Language PowerShell

# 4.  ─── Routes ───────────────────────────────────────────────

# /visit   (increments Visits)
Add-KrMapRoute -Verbs Get -Pattern '/visit' -ScriptBlock {
    $Visits['Count']++
    Write-KrTextResponse "🔢 Visits now: $($Visits['Count'])" 200
}

# /schedule/report   (JSON snapshot)
Add-KrMapRoute -Verbs Get -Pattern '/schedule/report' -ScriptBlock {
    $report = Get-KrScheduleReport
    Write-KrJsonResponse -InputObject $report -StatusCode 200
}

# 5.  ─── Start & shutdown loop ────────────────────────────────

Start-KrServer -Server $server
