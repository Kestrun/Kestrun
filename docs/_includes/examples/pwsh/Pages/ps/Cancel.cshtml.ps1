param()

# Read query params
$seconds = 0
$stepMs  = 500

try { $seconds = [int]($Context.Request.Query["seconds"]) } catch { }
try { $stepMs  = [int]($Context.Request.Query["stepMs"]) } catch { }

if ($seconds -le 0) { $seconds = 60 }
if ($stepMs  -le 0) { $stepMs  = 500 }

$steps = [Math]::Ceiling(($seconds * 1000) / $stepMs)
$sb = New-Object System.Text.StringBuilder

$startedAt = [DateTime]::UtcNow
$aborted = $false

$null = $sb.AppendLine("Started: $startedAt (UTC)")
$null = $sb.AppendLine("Requested seconds=$seconds; stepMs=$stepMs; steps=$steps")
$null = $sb.AppendLine("RequestAborted can be checked via: `$Context.RequestAborted.IsCancellationRequested")
$null = $sb.AppendLine("----")

for ($i = 1; $i -le $steps; $i++) {

    # Cooperative cancellation check
    if ($Context.RequestAborted.IsCancellationRequested) {
        $aborted = $true
        $null = $sb.AppendLine("ABORT OBSERVED at step $i / $steps, UTC=$([DateTime]::UtcNow.ToString('o'))")
        break
    }

    $null = $sb.AppendLine("Step $i/$steps; UTC=$([DateTime]::UtcNow.ToString('o'))")
    Start-Sleep -Milliseconds $stepMs
}

$endedAt = [DateTime]::UtcNow
$null = $sb.AppendLine("----")
$null = $sb.AppendLine("Ended: $endedAt (UTC)")
$null = $sb.AppendLine("Duration: $([int]($endedAt - $startedAt).TotalSeconds)s")
$null = $sb.AppendLine("Aborted: $aborted")

# IMPORTANT: set $Model (your middleware copies it into HttpContext.Items["PageModel"])
$Model = [pscustomobject]@{
    Started  = $true
    Aborted  = $aborted
    Seconds  = $seconds
    StepMs   = $stepMs
    Steps    = ($aborted ? ($i - 1) : $steps)
    Message  = ($aborted ? "Client aborted; script noticed and stopped." : "Completed normally.")
    Progress = $sb.ToString()
}
