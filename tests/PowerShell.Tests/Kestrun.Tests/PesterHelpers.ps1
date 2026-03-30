[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
param()
<#
.SYNOPSIS
    Shared helper functions for tutorial example tests.
.DESCRIPTION
    Provides utilities to locate example scripts, start them on a random port, collect routes, and stop.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Write-Debug '[TutorialHelper] Loading helper script' -Verbose

<#
.SYNOPSIS
    Locate the repository root directory.
.DESCRIPTION
    Walks up the directory tree from the current script location to find the repository root
    (identified by presence of Kestrun.sln).
    Throws if the root cannot be found.
.OUTPUTS
    String path to repository root directory.
#>
function Get-ProjectRootDirectory {
    [CmdletBinding()]
    param()
    $root = (Get-Item (Split-Path -Parent -Path $PSCommandPath))
    while ($root -and -not (Test-Path (Join-Path $root.FullName 'Kestrun.sln'))) {
        $parent = Get-Item (Split-Path -Parent -Path $root.FullName) -ErrorAction SilentlyContinue
        if (-not $parent -or $parent.FullName -eq $root.FullName) { break }
        $root = $parent
    }
    if (-not $root) { throw 'Cannot find repository root.' }
    return $root.FullName
}
<#
.SYNOPSIS
    Locate the tutorial examples directory.
.DESCRIPTION
    Walks up the directory tree from the current script location to find the repository root
    (identified by presence of Kestrun.sln), then appends 'docs/_includes/examples/pwsh'.
    Throws if the root or examples directory cannot be found.
.OUTPUTS
    String path to examples directory.
#>
function Get-TutorialExamplesDirectory {
    [CmdletBinding()]
    param()
    $root = (Get-Item (Split-Path -Parent -Path $PSCommandPath))
    while ($root -and -not (Test-Path (Join-Path $root.FullName 'Kestrun.sln'))) {
        $parent = Get-Item (Split-Path -Parent -Path $root.FullName) -ErrorAction SilentlyContinue
        if (-not $parent -or $parent.FullName -eq $root.FullName) { break }
        $root = $parent
    }
    if (-not $root) { throw 'Cannot find repository root.' }
    $examples = Join-Path (Join-Path (Join-Path (Join-Path $root.FullName 'docs') '_includes') 'examples') 'pwsh'
    if (-not (Test-Path $examples)) { throw "Examples directory not found: $examples" }
    return $examples
}

<#
.SYNOPSIS
    Get the full path to the Kestrun PowerShell module.
.DESCRIPTION
    Walks up the directory tree from the current script location to find the repository root
    (identified by presence of Kestrun.sln), then appends 'src/PowerShell/Kestrun/Kestrun.psm1'.
    Throws if the root or module file cannot be found.
.OUTPUTS
    String full path to Kestrun.psm1.
#>
function Get-KestrunModulePath {
    [CmdletBinding()]
    param()
    $root = (Get-Item (Split-Path -Parent -Path $PSCommandPath))
    while ($root -and -not (Test-Path (Join-Path $root.FullName 'Kestrun.sln'))) {
        $parent = Get-Item (Split-Path -Parent -Path $root.FullName) -ErrorAction SilentlyContinue
        if (-not $parent -or $parent.FullName -eq $root.FullName) { break }
        $root = $parent
    }
    if (-not $root) { throw 'Cannot find repository root.' }
    $kestrun = Join-Path -Path $root.FullName -ChildPath 'src' -AdditionalChildPath 'PowerShell', 'Kestrun', 'Kestrun.psm1'
    if (-not (Test-Path $kestrun)) { throw "Kestrun module not found: $kestrun" }
    return $kestrun
}

<#
.SYNOPSIS
    Get a free TCP port on localhost.
.DESCRIPTION
    Opens a TcpListener on port 0 to have the OS assign a free port, then closes it and returns the port number.
.OUTPUTS
    Integer port number.
#>
function Get-FreeTcpPort {
    [CmdletBinding()]
    [outputtype([int])]
    param(
        [int]$FallbackPort = 52000,
        [int]$MaxPort = 65102
    )

    if ($FallbackPort -lt 1 -or $FallbackPort -gt 65535) {
        throw "FallbackPort must be between 1 and 65535. Got: $FallbackPort"
    }

    if ($MaxPort -lt $FallbackPort -or $MaxPort -gt 65535) {
        throw "MaxPort must be between FallbackPort and 65535. Got: $MaxPort"
    }

    $listener = $null
    $rangeWidth = ($MaxPort - $FallbackPort) + 1
    $getRandomizedCandidateSequence = {
        $startOffset = if ($rangeWidth -gt 1) { Get-Random -Minimum 0 -Maximum $rangeWidth } else { 0 }
        for ($scanOffset = 0; $scanOffset -lt $rangeWidth; $scanOffset++) {
            $FallbackPort + (($startOffset + $scanOffset) % $rangeWidth)
        }
    }

    try {
        $retryCount = 0

        # OS-assigned ephemeral port probing.
        # Keep trying until we get a port in range; never return an out-of-range value.
        do {
            if ($null -ne $listener) {
                $listener.Stop()
                $listener.Dispose()
            }
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            $port = ($listener.LocalEndpoint).Port
            $retryCount++
        } until (((($port -ge $FallbackPort) -and ($port -le $MaxPort)) -or ($retryCount -ge 50)))

        if (($port -ge $FallbackPort) -and ($port -le $MaxPort)) {
            return $port
        }

        # Randomize the fallback scan start to reduce collisions when many tests are provisioning ports concurrently.
        foreach ($candidate in (& $getRandomizedCandidateSequence)) {
            if ($null -ne $listener) {
                $listener.Stop()
                $listener.Dispose()
                $listener = $null
            }

            try {
                $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $candidate)
                $listener.Start()
                return $candidate
            } catch {
                continue
            }
        }

        throw "Unable to find a free TCP port between $FallbackPort and $MaxPort"
    } catch {
        Write-Warning "Failed to get free TCP port via primary probe: $_. Retrying randomized scan."

        foreach ($candidate in (& $getRandomizedCandidateSequence)) {
            if ($null -ne $listener) {
                $listener.Stop()
                $listener.Dispose()
                $listener = $null
            }

            try {
                $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $candidate)
                $listener.Start()
                return $candidate
            } catch {
                continue
            }
        }

        throw "Unable to find a free TCP port between $FallbackPort and $MaxPort after randomized fallback scan"
    } finally {
        if ($null -ne $listener) {
            $listener.Stop()
            $listener.Dispose()
        }
    }
}

<#
    .SYNOPSIS
        Finds the first free contiguous block of TCP ports on loopback.
    .DESCRIPTION
        Returns the starting port for a block of consecutive ports that can all
        be bound on 127.0.0.1 at the time of selection. Each port in the block
        must be available for both TCP and UDP so mixed HTTP/TLS/QUIC examples
        can safely derive sibling listeners from the base `-Port` value, for
        example `$Port + 1`, `$Port + 2`, and so on.
    .PARAMETER Count
        Number of consecutive ports required in the returned block.
    .PARAMETER FallbackPort
        Lowest candidate starting port to consider.
    .PARAMETER MaxPort
        Highest candidate starting port to consider.
#>
function Get-FreeTcpPortBlock {
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [ValidateRange(1, 256)]
        [int]$Count = 1,
        [int]$FallbackPort = 52000,
        [int]$MaxPort = 65102
    )

    if ($Count -le 1) {
        return Get-FreeTcpPort -FallbackPort $FallbackPort -MaxPort $MaxPort
    }

    if ($FallbackPort -lt 1 -or $FallbackPort -gt 65535) {
        throw "FallbackPort must be between 1 and 65535. Got: $FallbackPort"
    }

    if ($MaxPort -lt $FallbackPort -or $MaxPort -gt 65535) {
        throw "MaxPort must be between FallbackPort and 65535. Got: $MaxPort"
    }

    $maxStartPort = $MaxPort - ($Count - 1)
    if ($maxStartPort -lt $FallbackPort) {
        throw "Port block size $Count does not fit inside the requested range $FallbackPort-$MaxPort."
    }

    $rangeWidth = ($maxStartPort - $FallbackPort) + 1
    $startOffset = if ($rangeWidth -gt 1) { Get-Random -Minimum 0 -Maximum $rangeWidth } else { 0 }
    $listeners = [System.Collections.Generic.List[System.Net.Sockets.TcpListener]]::new()
    $udpClients = [System.Collections.Generic.List[System.Net.Sockets.UdpClient]]::new()
    $stopTcpListener = {
        param([System.Net.Sockets.TcpListener]$Listener)

        if (-not $Listener) {
            return
        }

        try {
            $Listener.Stop()
        } catch {
            Write-Verbose ("Ignoring TCP listener stop error during port-block cleanup: {0}" -f $_.Exception.Message)
        }

        try {
            $Listener.Dispose()
        } catch {
            Write-Verbose ("Ignoring TCP listener dispose error during port-block cleanup: {0}" -f $_.Exception.Message)
        }
    }
    $disposeUdpClient = {
        param([System.Net.Sockets.UdpClient]$UdpClient)

        if (-not $UdpClient) {
            return
        }

        try {
            $UdpClient.Dispose()
        } catch {
            Write-Verbose ("Ignoring UDP client dispose error during port-block cleanup: {0}" -f $_.Exception.Message)
        }
    }

    try {
        for ($scanOffset = 0; $scanOffset -lt $rangeWidth; $scanOffset++) {
            $candidateStart = $FallbackPort + (($startOffset + $scanOffset) % $rangeWidth)
            $listeners.Clear()
            $blockAvailable = $true

            for ($portOffset = 0; $portOffset -lt $Count; $portOffset++) {
                $candidatePort = $candidateStart + $portOffset
                $listener = $null
                $udpClient = $null
                try {
                    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $candidatePort)
                    $listener.Start()
                    $listeners.Add($listener)

                    $udpClient = [System.Net.Sockets.UdpClient]::new($candidatePort)
                    $udpClients.Add($udpClient)
                } catch {
                    if ($listener) {
                        & $stopTcpListener $listener
                    }
                    if ($udpClient) { & $disposeUdpClient $udpClient }
                    $blockAvailable = $false
                    break
                }
            }

            foreach ($listener in $listeners) {
                & $stopTcpListener $listener
            }
            $listeners.Clear()
            foreach ($udpClient in $udpClients) {
                & $disposeUdpClient $udpClient
            }
            $udpClients.Clear()

            if ($blockAvailable) {
                return $candidateStart
            }
        }
    } finally {
        foreach ($listener in $listeners) {
            & $stopTcpListener $listener
        }
        foreach ($udpClient in $udpClients) {
            & $disposeUdpClient $udpClient
        }
    }

    throw "No free contiguous TCP port block of size $Count was found between $FallbackPort and $MaxPort."
}

<#
.SYNOPSIS
    Get the full path to a tutorial example script by name.
.DESCRIPTION
    Uses Get-TutorialExamplesDirectory to locate the examples directory, then appends the provided script name.
    Throws if the script cannot be found.
.PARAMETER Name
    The name of the example script file (e.g. '3.2-File-Server.ps1').
.OUTPUTS
    String full path to the example script.
#>
function Get-ExampleScriptPath {
    [CmdletBinding()] param([Parameter(Mandatory)][string]$Name)
    $examples = Get-TutorialExamplesDirectory
    $full = Join-Path $examples $Name
    if (-not (Test-Path $full)) { throw "Example script not found: $Name" }
    return $full
}

<#
.SYNOPSIS
    Resolve the current PowerShell executable path.
.DESCRIPTION
    Returns the path of the currently running PowerShell process when available.
    Falls back to resolving `pwsh` via Get-Command.
.OUTPUTS
    String full path to pwsh executable.
#>
function Get-PwshExecutable {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $pwshExecutable = (Get-Process -Id $PID -ErrorAction Stop).Path
    if ([string]::IsNullOrWhiteSpace($pwshExecutable)) {
        $pwshExecutable = (Get-Command pwsh -ErrorAction Stop).Source
    }

    return $pwshExecutable
}


<#
.SYNOPSIS
    Start a tutorial example script in a background process on a free TCP port.
.DESCRIPTION
    Locates the example script by name, modifies it to listen on a free port (unless specified),
    injects a shutdown route, writes it to a temp file, and starts it in a background pwsh process.
    Polls the process for readiness by checking for open port and/or startup sentinels in stdout.
    Returns an object with details about the running instance.
.PARAMETER Name
    The name of the example script file (e.g. '3.2-File-Server.ps1').
.PARAMETER ScriptBlock
    Alternatively to Name, provide a script block containing the example script code to run.
.PARAMETER Port
    Optional explicit port number to use. If not provided, a free port will be selected.
.PARAMETER StartupTimeoutSeconds
        Maximum time to wait for the example to start accepting connections. Default is 40 seconds.
.PARAMETER HttpProbeDelayMs
    Delay between HTTP probes of the root URL when waiting for startup. Default is 150ms.
    .PARAMETER SkipPortProbe
        If specified, skips readiness probing and returns immediately after starting the child process.
.PARAMETER FromRootDirectory
    If specified, resolves example script paths relative to the repository root instead of the module directory.
    .PARAMETER EnvironmentVariables
        Environment variable names to copy from the current process into the child example process.
.OUTPUTS
    A custom object with properties:
    - Name: The name of the example script.
        - BaseName: The script file name without extension, or 'ScriptBlock' when started from a script block.
        - Url: Base URL for the started example using the detected scheme, host, and port.
        - Host: The host name used for startup and request probing.
    - Port: The TCP port number the example is listening on.
    - StartupAttemptCount: Number of startup attempts made before returning.
    - PortRetryCount: Number of retries caused by transient startup conflicts.
        - PortWasProvided: Indicates whether the caller explicitly supplied the port.
    - PortsTried: Ports used across startup attempts.
        - LastStartupProbeError: Last readiness probe error captured while waiting for startup, if any.
    - TempPath: The path to the temporary modified script file.
    - Process: The Process object of the started example.
    - Content: The modified script content that was run.
    - StdOut: Path to the redirected standard output log file.
    - StdErr: Path to the redirected standard error log file.
    - ExitedEarly: Boolean indicating if the process exited before startup completed.
    - Ready: Boolean indicating if the example is ready to accept connections.
        - ScriptDirectory: Working directory used when launching the example.
        - OriginalLocation: Caller location captured before changing into the example directory.
        - PushedLocation: Indicates whether the helper pushed a location that Stop-ExampleScript should pop.
        - Https: Indicates whether the helper inferred HTTPS for the started example.
#>
function Start-ExampleScript {
    [CmdletBinding(SupportsShouldProcess, defaultParameterSetName = 'Name')]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Name')]
        [string]$Name,
        [Parameter(Mandatory = $true, ParameterSetName = 'ScriptBlock')]
        [scriptblock]$ScriptBlock,
        [int]$Port,
        [ValidateRange(1, 256)]
        [int]$PortCount = 1,
        [int]$StartupTimeoutSeconds = 40,
        [int]$HttpProbeDelayMs = 150,
        [switch]$SkipPortProbe,
        [switch]$FromRootDirectory,
        [string[]]$EnvironmentVariables = @('UPSTASH_REDIS_URL')
    )
    if (-not $Port) { $Port = Get-FreeTcpPortBlock -Count $PortCount }
    if ($PSCmdlet.ParameterSetName -eq 'Name') {
        if ( $FromRootDirectory ) {
            $root = Resolve-Path "$PSScriptRoot\..\..\.."
            $path = Join-Path $root $Name
            if (-not (Test-Path $path)) { throw "Example script not found: $Name" }
        } else {
            $path = Get-ExampleScriptPath -Name $Name
        }
        $scriptDir = Split-Path -Parent -Path $path
        $originalLocation = Get-Location
        # Push current location so relative file references (Assets/...) resolve inside example
        Push-Location -Path $scriptDir
        $content = Get-Content -Path $path -Raw
        $pushedLocation = $true
    } else {
        $content = $ScriptBlock.ToString()
        $pushedLocation = $false
        $scriptDir = $PSScriptRoot
        $originalLocation = $scriptDir
    }
    $serverIp = 'localhost' # Use loopback for safety

    $kestrunModulePath = Get-KestrunModulePath
    # Inject /shutdown and /online endpoints if not already present
    if (-not $content.Contains('-Pattern "/shutdown"')) {
        # Inject shutdown endpoint for legacy scripts (first occurrence of Start-KrServer)

        $content = [System.Text.RegularExpressions.Regex]::Replace(
            $content,
            '\bStart-KrServer\b', @'
Add-KrMapRoute -Verbs Get -Pattern "/shutdown" -ScriptBlock { Stop-KrServer }
Add-KrMapRoute -Verbs Get -Pattern "/online" -ScriptBlock { Write-KrTextResponse -InputObject 'OK' -StatusCode 200 }
Start-KrServer
'@
            , 1 # only first occurrence
        )
    }


    # Adjust Initialize-KrRoot if present to the example script directory
    if ( $content.Contains('Initialize-KrRoot -Path $PSScriptRoot')) {
        $content = $content.Replace('Initialize-KrRoot -Path $PSScriptRoot', "Initialize-KrRoot -Path '$scriptDir'")
    }

    # Heuristic: detect HTTPS usage if listener line includes cert/self-signed flags.
    # This is also used to avoid noisy HTTPS readiness probes for HTTP-only examples.
    $usesHttps = $false
    if (($content -match 'Add-KrEndpoint[^\n]*-SelfSignedCert') -or
        ($content -match 'Add-KrEndpoint[^\n]*-CertPath') -or
        ($content -match 'Add-KrEndpoint[^\n]*-X509Certificate')
    ) {
        $usesHttps = $true
    }

    $tempDir = [System.IO.Path]::GetTempPath()
    # Generate a unique file name for the temp script
    $fileNameWithoutExtension = ([string]::IsNullOrEmpty($Name)) ? 'ScriptBlockExample' : [IO.Path]::GetFileNameWithoutExtension((Split-Path -Leaf $Name))

    # Write modified legacy content to temp file
    $scriptToRun = Join-Path $tempDir ('kestrun-example-' + $fileNameWithoutExtension + '-' + [System.IO.Path]::GetRandomFileName() + '.ps1')
    $exampleIdentifier = if (-not [string]::IsNullOrWhiteSpace($Name)) { $Name } else { $fileNameWithoutExtension }
    Set-Content -Path $scriptToRun -Value $content -Encoding UTF8

    $stdOut = $null
    $stdErr = $null
    $proc = $null
    $ready = $false
    $exited = $false
    $errorMessage = $null
    $portWasProvided = $PSBoundParameters.ContainsKey('Port')
    $maxStartupAttempts = if ($portWasProvided) { 1 } else { 3 }
    $startupAttempt = 0
    $portRetryCount = 0
    $portsTried = [System.Collections.Generic.List[int]]::new()

    # Build environment variables for the child process (including UPSTASH_REDIS_URL if set in parent)
    $environment = @{}
    # Copy current environment variables
    foreach ($key in [System.Environment]::GetEnvironmentVariables().Keys) {
        if ($EnvironmentVariables -contains $key) {
            $environment[$key] = [System.Environment]::GetEnvironmentVariable($key)
        }
    }

    $pwshExecutable = Get-PwshExecutable

    $newLogFilePath = {
        param([string]$suffix)
        Join-Path $tempDir ('kestrun-example-' + $fileNameWithoutExtension + '-' + [System.IO.Path]::GetRandomFileName() + $suffix)
    }

    $isPortInUseFailure = {
        param(
            [string]$StdOutPath,
            [string]$StdErrPath
        )

        $combined = ''
        if ($StdOutPath -and (Test-Path $StdOutPath)) {
            $combined += (Get-Content -Path $StdOutPath -Raw)
            $combined += "`n"
        }
        if ($StdErrPath -and (Test-Path $StdErrPath)) {
            $combined += (Get-Content -Path $StdErrPath -Raw)
        }

        return $combined -match '(?im)address already in use|only one usage of each socket address'
    }

    do {
        $startupAttempt++
        if (-not $portWasProvided -and $startupAttempt -gt 1) {
            $Port = Get-FreeTcpPortBlock -Count $PortCount
        }

        $portsTried.Add($Port)

        $stdOut = & $newLogFilePath '.out.log'
        $stdErr = & $newLogFilePath '.err.log'

        # This becomes: <current-pwsh> -NoLogo -NoProfile -Command "Import-Module Kestrun; . 'C:\...\myscript.ps1'"
        $argList = @(
            '-NoLogo'
            '-NoProfile'
            '-Command'
            "Import-Module '$kestrunModulePath'; . '$scriptToRun' -Port $Port"
        )

        # Create process start parameters
        $param = @{
            FilePath = $pwshExecutable
            WorkingDirectory = $scriptDir
            ArgumentList = $argList
            PassThru = $true
            RedirectStandardOutput = $stdOut
            RedirectStandardError = $stdErr
            Environment = $environment
        }

        # Prevent spawned process from inheriting the test runner's console window on Windows (avoids unwanted UI popups during automated tests)
        if ($IsWindows) { $param.WindowStyle = 'Hidden' }

        $proc = Start-Process @param

        # Wait for the process to start accepting connections or timeout
        $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
        $ready = $SkipPortProbe.IsPresent
        $attempt = 0
        $errorMessage = $null
        if (-not $SkipPortProbe.IsPresent) {
            Start-Sleep -Seconds 1 # Initial delay before probing
            while ([DateTime]::UtcNow -lt $deadline -and -not $ready) {
                if ($proc.HasExited) { break }
                $attempt++
                # Optional lightweight HTTP/HTTPS probe of '/' and '/online' endpoints to detect readiness.
                # Probe HTTP first because many examples are HTTP-only or become reachable over HTTP before HTTPS.
                $probeUrls = @(
                    "http://$serverIp`:$Port/online",
                    "http://$serverIp`:$Port/"
                )
                if ($usesHttps) {
                    $probeUrls += @(
                        "https://$serverIp`:$Port/online",
                        "https://$serverIp`:$Port/"
                    )
                }
                foreach ($url in $probeUrls) {
                    try {
                        $probe = Get-HttpHeadersRaw -Uri $url -Insecure -AsHashtable -TimeoutSeconds 2
                        if ($probe.StatusCode -ge 200 -and $probe.StatusCode -lt 600) {
                            $ready = $true
                            break
                        }
                    } catch {
                        $errorMessage = $_.Exception.Message
                    }
                }
                Start-Sleep -Milliseconds $HttpProbeDelayMs
            }

            # Some HTTPS examples on Windows become reachable to Invoke-WebRequest
            # slightly before or after the raw socket probe can confirm readiness.
            # Before warning, do one final short, high-level probe using the same
            # client stack the tests use so we reduce false negatives without
            # reintroducing unbounded waits.
            if (-not $ready -and -not $proc.HasExited) {
                $finalProbeUrls = @()
                if ($usesHttps) {
                    $finalProbeUrls += @(
                        "https://$serverIp`:$Port/online",
                        "https://$serverIp`:$Port/"
                    )
                }
                $finalProbeUrls += @(
                    "http://$serverIp`:$Port/online",
                    "http://$serverIp`:$Port/"
                )

                foreach ($url in ($finalProbeUrls | Select-Object -Unique)) {
                    try {
                        $finalProbe = Invoke-WebRequest -Uri $url -Method Get -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 5
                        if ($null -ne $finalProbe -and $finalProbe.StatusCode -ge 200 -and $finalProbe.StatusCode -lt 600) {
                            $ready = $true
                            $errorMessage = $null
                            break
                        }
                    } catch {
                        $errorMessage = $_.Exception.Message
                    }
                }
            }
        }

        $exited = $proc.HasExited
        if ($exited -and -not $portWasProvided -and $startupAttempt -lt $maxStartupAttempts -and (& $isPortInUseFailure $stdOut $stdErr)) {
            $portRetryCount++
            Write-Warning "Example $exampleIdentifier failed to bind to port $Port on startup attempt $startupAttempt of $maxStartupAttempts; retrying with a new port."
            $proc.Dispose()
            $proc = $null
            Remove-Item -Path $stdOut -Force -ErrorAction SilentlyContinue
            Remove-Item -Path $stdErr -Force -ErrorAction SilentlyContinue
            continue
        }

        break
    } while ($startupAttempt -lt $maxStartupAttempts)

    if (-not $ready -and -not $exited) {
        if ($errorMessage) {
            Write-Warning "Example $exampleIdentifier not accepting connections on port $Port after timeout. Last probe error: $errorMessage. Continuing; requests may fail."
        } else {
            Write-Warning "Example $exampleIdentifier not accepting connections on port $Port after timeout. Continuing; requests may fail."
        }
    }

    if ($exited) {
        $attemptSummary = if ($portsTried.Count -gt 0) { " Attempts=$startupAttempt Ports=$($portsTried -join ',')" } else { '' }
        Write-Warning "Example $exampleIdentifier process exited early with code $($proc.ExitCode).$attemptSummary Capturing logs."
        if (Test-Path $stdErr) { Write-Warning ('stderr: ' + (Get-Content $stdErr -Raw)) }
        if (Test-Path $stdOut) { Write-Verbose ('stdout: ' + (Get-Content $stdOut -Raw)) -Verbose }
    }

    Start-Sleep -Seconds 2 # Allow some time for server to stabilize

    return [pscustomobject]@{
        Name = $Name
        BaseName = (-not [string]::IsNullOrWhiteSpace($Name))? [System.IO.Path]::GetFileNameWithoutExtension($Name): 'ScriptBlock'
        Url = ('{0}://{1}:{2}' -f ($usesHttps ? 'https' : 'http'), $serverIp, $Port)
        Host = $serverIp
        Port = $Port
        StartupAttemptCount = $startupAttempt
        PortRetryCount = $portRetryCount
        PortWasProvided = $portWasProvided
        PortsTried = @($portsTried)
        LastStartupProbeError = $errorMessage
        TempPath = $scriptToRun
        Process = $proc
        Content = $content
        StdOut = $stdOut
        StdErr = $stdErr
        ExitedEarly = $exited
        Ready = $ready
        ScriptDirectory = $scriptDir
        OriginalLocation = $originalLocation
        PushedLocation = $pushedLocation
        Https = $usesHttps
    }
}

<#
.SYNOPSIS
    Stop a running tutorial example script instance.
.DESCRIPTION
    Sends a shutdown request to the example's /shutdown endpoint, then forcibly kills the process if still running.
    Cleans up temporary files created for the example.
.PARAMETER Instance
    The object returned by Start-ExampleScript representing the running instance to stop.
#>
function Stop-ExampleScript {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        $Instance
    )
    $processId = $null
    if ($null -ne $Instance.Process -and $Instance.Process.PSObject.Properties.Match('Id').Count -gt 0) {
        $processId = [int]$Instance.Process.Id
    }

    $shutdown = "http://127.0.0.1:$($Instance.Port)/shutdown"
    try { Invoke-WebRequest -Uri $shutdown -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop | Out-Null } catch {
        try {
            Write-Debug "Initial shutdown failed, retrying with HTTPS: $($_.Exception.Message)"
            $shutdown = "https://127.0.0.1:$($Instance.Port)/shutdown"
            Invoke-WebRequest -Uri $shutdown -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop -SkipCertificateCheck | Out-Null
        } catch {
            Write-Debug "Shutdown failed: $($_.Exception.Message)"
        }
    } finally {
        if ($processId) {
            $liveProcess = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($liveProcess) {
                # Give graceful shutdown a short chance before forcing termination.
                $null = $liveProcess.WaitForExit(3000)
            }

            $liveProcess = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($liveProcess) {
                try {
                    $liveProcess.Kill($true)
                } catch {
                    Write-Debug "Kill(processTree) failed for PID $processId, falling back to Stop-Process: $($_.Exception.Message)"
                    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                }
            }

            $deadline = [DateTime]::UtcNow.AddSeconds(5)
            while (([DateTime]::UtcNow -lt $deadline) -and (Get-Process -Id $processId -ErrorAction SilentlyContinue)) {
                Start-Sleep -Milliseconds 100
            }

            if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
                Write-Warning "Process PID $processId is still running after forced termination attempts."
            }
        }

        $p = [pscustomobject]@{
            Id = $processId
            HasExited = $null
            ExitCode = $null
        }
        if ($null -ne $Instance.Process) {
            try {
                $p.HasExited = $Instance.Process.HasExited
                if ($p.HasExited) {
                    $p.ExitCode = $Instance.Process.ExitCode
                }
            } catch {
                Write-Debug "Unable to capture final process status for PID ${processId}: $($_.Exception.Message)"
            }

            try {
                $Instance.Process.Dispose()
            } catch {
                Write-Debug "Dispose failed for PID ${processId}: $($_.Exception.Message)"
            }
        }

        $Instance.Process = $p
        Remove-Item -Path $Instance.TempPath -Force -ErrorAction SilentlyContinue
        if ($Instance.PushedLocation) {
            try { Pop-Location -ErrorAction Stop } catch { Write-Warning "Pop-Location failed: $($_.Exception.Message)" }
        }
    }
}

<#
.SYNOPSIS
    Extract route patterns from example script content.
.DESCRIPTION
    Uses regex to find all occurrences of Add-KrMapRoute with -Pattern or -Path parameters,
    returning a unique sorted list of route patterns (excluding /shutdown).
.PARAMETER ScriptContent
    The full text content of the example script.
.OUTPUTS
    Array of unique route pattern strings.
#>
function Get-ExampleRoutePattern {
    [CmdletBinding()] param([Parameter(Mandatory)][string]$ScriptContent)
    $routes = @()
    # Support both -Pattern and -Path parameters for route definitions
    $pattern = 'Add-KrMapRoute\b[^\n\r]*?-(?:Pattern|Path)\s+"([^"]+)"'
    foreach ($m in [regex]::Matches($ScriptContent, $pattern)) { $routes += $m.Groups[1].Value }
    $routes | Where-Object { $_ -and $_ -ne '/shutdown' } | Sort-Object -Unique
}

<#
.SYNOPSIS
    Convert a route pattern to a full URL using the provided port.
.DESCRIPTION
    Replaces route parameters (e.g. {id}, {*path}) with 'sample', ensures leading slash,
    and constructs a full URL with "$($Scheme)://$($Server):$Port" prefix.
.PARAMETER Scheme
    The URL scheme to use (http or https). Default is 'http'.
.PARAMETER Server
    The server address to use. Default is '127.0.0.1'.
.PARAMETER Route
    The route pattern string (e.g. '/items/{id}').
.PARAMETER Port
    The TCP port number to use in the URL.
.OUTPUTS
    String full URL (e.g. 'http://127.0.0.1:5000/items/sample').
#>
function Convert-RouteToUrl {
    [CmdletBinding()]
    [outputtype([string])]
    param(
        [string]$Scheme = 'http',
        [string]$Server = '127.0.0.1',
        [string]$Route,
        [int]$Port)
    $r = [regex]::Replace($Route, '{\*?[^}]+}', 'sample')
    if (-not $r.StartsWith('/')) { $r = '/' + $r }
    return "$($Scheme)://$($Server):$Port$r"
}

<#
.SYNOPSIS
    Wait until an example route starts responding successfully.
.DESCRIPTION
    Polls a specific route on a started example instance until it returns one of
    the expected status codes or the timeout elapses.
.PARAMETER Instance
    The object returned by Start-ExampleScript.
.PARAMETER Route
    The route to probe, for example '/online' or '/status'.
.PARAMETER ExpectedStatus
    One or more acceptable HTTP status codes.
.PARAMETER TimeoutSeconds
    Maximum time to wait for the route to become available.
.PARAMETER RetryDelayMs
    Delay between probe attempts.
.OUTPUTS
    The successful Invoke-WebRequest response object.
#>
function Wait-ExampleRoute {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Instance,
        [Parameter(Mandatory)] [string] $Route,
        [int[]] $ExpectedStatus = @(200),
        [int] $TimeoutSeconds = 20,
        [int] $RetryDelayMs = 250
    )

    $scheme = if ($Instance.Https) { 'https' } else { 'http' }
    $uri = Convert-RouteToUrl -Scheme $scheme -Route $Route -Port $Instance.Port
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = $null

    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $invokeParams = @{
                Uri = $uri
                UseBasicParsing = $true
                TimeoutSec = [Math]::Min(5, $TimeoutSeconds)
                Method = 'Get'
            }
            if ($Instance.Https) {
                $invokeParams.SkipCertificateCheck = $true
            }

            $response = Invoke-WebRequest @invokeParams
            if ($ExpectedStatus -contains $response.StatusCode) {
                return $response
            }

            $lastError = "Route $Route returned status $($response.StatusCode)"
        } catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds $RetryDelayMs
    }

    throw "Timed out waiting for route $Route on port $($Instance.Port). Last error: $lastError"
}

<#
.SYNOPSIS
    Test all routes defined in an example script for 200 response.
.DESCRIPTION
    Extracts route patterns from the provided script content, converts them to URLs using the instance's port,
    and issues GET requests to each. Skips routes matching any of the provided skip patterns.
    Optionally allows custom invokers per route, content expectations, and body non-empty assertion.
.PARAMETER Instance
    The object returned by Start-ExampleScript representing the running instance to test.
.PARAMETER SkipPatterns
    Array of regex patterns; any route matching one will be skipped. Default skips common non-content routes.
.PARAMETER CustomInvokers
    Hashtable mapping route patterns to scriptblocks that take parameters (Url, Port, Route, Instance)
    and perform custom request logic. If provided, the scriptblock is invoked instead of the default
    GET request for that route.
.PARAMETER ContentExpectations
    Hashtable mapping route patterns to expected content checks. Each value can be:
    - A string: asserts the response body contains that substring.
    - A scriptblock: invoked with parameters (Response, Route, Instance) for custom assertions.
    - A hashtable with keys 'Contains', 'Regex', or 'Exact' for specific assertions.
.PARAMETER AssertBodyNotEmpty
    If set, asserts that the response body is not empty for routes without specific content expectations.
    This helps catch cases where a route might return a 200 status but no content.
.OUTPUTS
    None. Uses Pester assertions to validate each route.
#>
function Test-ExampleRouteSet {
    [CmdletBinding()] param(
        [Parameter(Mandatory)]$Instance,
        [string[]] $SkipPatterns = @('favicon', 'redirect', 'error', 'login', 'logout'),
        [hashtable] $CustomInvokers,
        [hashtable] $ContentExpectations,
        [switch] $AssertBodyNotEmpty
    )
    $routes = Get-ExampleRoutePattern -ScriptContent $Instance.Content
    foreach ($r in $routes) {
        if ($SkipPatterns | Where-Object { $r -match $_ }) { continue }
        if ($r -match '{\*[^}]*}') { continue } # Skip wildcard routes for simplicity
        $url = Convert-RouteToUrl -Route $r -Port $Instance.Port
        $method = 'Get'
        $body = $null
        $headers = @{}
        if ($r -like '/input/{value}' -or $r -like '/{value}') { $url = $url -replace 'sample$', 'demoValue'; $method = 'Get' }

        if ($CustomInvokers -and $CustomInvokers.ContainsKey($r)) {
            & $CustomInvokers[$r] -Url $url -Port $Instance.Port -Route $r | Out-Null
            continue
        }

        Write-Verbose "Testing $($Instance.Name): $r -> $url ($method)"
        if ($Instance.Https) {
            $url = $url -replace '^http:', 'https:'
            $invokeParams = @{ Uri = $url; UseBasicParsing = $true; TimeoutSec = 8; Method = $method; Headers = $headers; Body = $body; SkipCertificateCheck = $true }
        } else {
            $invokeParams = @{ Uri = $url; UseBasicParsing = $true; TimeoutSec = 8; Method = $method; Headers = $headers; Body = $body }
        }
        $resp = Invoke-WebRequest @invokeParams
        if ($resp.StatusCode -ne 200) { throw "Route $r returned status $($resp.StatusCode)" }

        # Content assertions
        if ($ContentExpectations -and $ContentExpectations.ContainsKey($r)) {
            $exp = $ContentExpectations[$r]
            if ($exp -is [string]) {
                ($resp.Content -like "*${exp}*") | Should -BeTrue -Because "Body should contain expected substring for route $r"
            } elseif ($exp -is [scriptblock]) {
                & $exp -Response $resp -Route $r -Instance $Instance
            } elseif ($exp -is [hashtable]) {
                if ($exp.ContainsKey('Contains')) { ($resp.Content -like "*${($exp.Contains)}*") | Should -BeTrue }
                if ($exp.ContainsKey('Regex')) { $resp.Content | Should -Match $exp.Regex }
                if ($exp.ContainsKey('Exact')) { $resp.Content | Should -Be $exp.Exact }
            } else {
                Write-Warning "Unsupported expectation type for route $($r): $($exp.GetType().FullName)"
            }
        } elseif ($AssertBodyNotEmpty) {
            ($resp.Content -and ($resp.Content.Trim().Length -gt 0)) | Should -BeTrue -Because "Body should not be empty for route $r"
        }
    }
}

<#
.SYNOPSIS
    Issue an HTTP request and validate status and optional content expectations.
.DESCRIPTION
    Issues a request to the specified URI with the given method, headers, body, and content type.
    Validates that the response status matches ExpectStatus (default 200).
    Optionally checks that the Content-Type header contains a specified substring,
    and/or that the response body contains a specified substring.
    Retries the request up to RetryCount times with a delay if it fails.
.PARAMETER Uri
    Fully qualified URL.
.PARAMETER Method
    HTTP verb (default GET).
.PARAMETER ExpectStatus
    Expected status (default 200).
.PARAMETER ContentTypeContains
    Substring expected to be present in the Content-Type header.
.PARAMETER BodyContains
    Substring expected to be present in the response body.
.PARAMETER Body
    Request body content (for POST, PUT, etc.).
.PARAMETER ContentType
    Content-Type header value for the request body.
.PARAMETER Headers
    Hashtable of additional headers to include in the request.
.PARAMETER ReturnRaw
    If set, returns the full Invoke-WebRequest response object; otherwise returns nothing on success.
.PARAMETER RetryCount
    Number of times to retry the request on failure (default 1).
.PARAMETER RetryDelayMs
    Delay in milliseconds between retries (default 250ms).
.OUTPUTS
    If ReturnRaw is set, returns the Invoke-WebRequest response object; otherwise returns nothing on
    success. Throws on failure.
#>
# Region: Assertion utilities
function Invoke-ExampleRequest {
    [CmdletBinding()] param(
        [Parameter(Mandatory)] [string] $Uri,
        [ValidateSet('Get', 'Post', 'Put', 'Patch', 'Delete', 'Head')] [string] $Method = 'Get',
        [int] $ExpectStatus = 200,
        [string] $ContentTypeContains,
        [string] $BodyContains,
        [object] $Body,
        [string] $ContentType,
        [hashtable] $Headers,
        [switch] $ReturnRaw,
        [int] $RetryCount = 1,
        [int] $RetryDelayMs = 250
    )
    $lastErr = $null
    for ($i = 0; $i -le $RetryCount; $i++) {
        try {
            $invokeParams = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true; TimeoutSec = 8 }
            if ($Uri -like 'https://*') { $invokeParams.SkipCertificateCheck = $true }
            if ($Headers) { $invokeParams.Headers = $Headers }
            if ($Body) { $invokeParams.Body = $Body }
            if ($ContentType) { $invokeParams.ContentType = $ContentType }
            $resp = Invoke-WebRequest @invokeParams
            $resp.StatusCode | Should -Be $ExpectStatus
            if ($ContentTypeContains) { ($resp.Headers['Content-Type'] -join ';') | Should -Match $ContentTypeContains }
            if ($BodyContains) { ($resp.Content -like "*${BodyContains}*") | Should -BeTrue -Because "Body should contain substring '${BodyContains}'" }
            if ($ReturnRaw) { return $resp } else { return }
        } catch {
            $lastErr = $_
            if ($i -lt $RetryCount) { Start-Sleep -Milliseconds $RetryDelayMs; continue } else { throw $lastErr }
        }
    }
}

<#
.SYNOPSIS
    Assert that a JSON field has the expected value.
.DESCRIPTION
    Parses the provided JSON string, extracts the specified field, and asserts that its value
    matches the expected value using string comparison.
.PARAMETER Json
    The JSON string to parse.
.PARAMETER Field
    The name of the field to extract.
.PARAMETER Expected
    The expected value of the field (string comparison).
#>
function Assert-JsonFieldValue {
    [CmdletBinding()] param(
        [Parameter(Mandatory)][string]$Json,
        [Parameter(Mandatory)][string]$Field,
        [Parameter(Mandatory)][string]$Expected
    )
    $obj = $Json | ConvertFrom-Json -ErrorAction Stop
    $actual = $obj.$Field
    $actual | Should -Be $Expected
}

<#
.SYNOPSIS
    Assert that a YAML string contains a key with the expected value.
.DESCRIPTION
    Searches the provided YAML string for a line matching 'key: value' (with optional whitespace).
    If the YAML appears to be numeric-per-line (e.g. ASCII codes), attempts to normalize it to characters first.
.PARAMETER Yaml
    The YAML string or object to search.
.PARAMETER Key
    The key to look for.
.PARAMETER Expected
    The expected value for the key.
#>
function Assert-YamlContainsKeyValue {
    [CmdletBinding()] param(
        [Parameter(Mandatory)][object]$Yaml,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Expected
    )
    $NUMERIC_LINE_DETECTION_THRESHOLD = 0.6
    $text = if ($Yaml -is [string]) { $Yaml } else { ($Yaml | Out-String) }
    # Normalize numeric-per-line payload (observed in YAML response) to characters
    $lines = ($text -split "`r?`n") | Where-Object { $_ -ne '' }
    $digitLines = $lines | Where-Object { $_ -match '^[0-9]+$' }
    if ($digitLines.Count -gt 0 -and $digitLines.Count -ge ($lines.Count * $NUMERIC_LINE_DETECTION_THRESHOLD)) {
        try {
            $chars = $digitLines | ForEach-Object { [char][int]$_ }
            $text = -join $chars
        } catch {
            Write-Debug ("Failed to normalize numeric YAML lines. Exception type: $($_.Exception.GetType().FullName). Message: $($_.Exception.Message). Input: " + ($digitLines -join ', '))
        }
    }
    $pattern = "^\s*${Key}:\s*${Expected}\s*$"
    ($text -split "`n") | Where-Object { $_ -match $pattern } | Should -Not -BeNullOrEmpty -Because "YAML should contain '${Key}: ${Expected}'"
}

<#
.SYNOPSIS
    Unified content assertion for a route.
.DESCRIPTION
    Issues a request and validates status plus one of: Exact, Contains, Regex,
    Json (field/value), or Yaml (key/value). Fails if none of the expectation
    parameters are provided.
.PARAMETER Uri
    Fully qualified URL.
.PARAMETER Method
    HTTP verb (default GET).
.PARAMETER ExpectStatus
    Expected status (default 200).
.PARAMETER Exact
    Exact body match string.
.PARAMETER Contains
    Substring expected anywhere in body.
.PARAMETER Regex
    Regex pattern expected to match body.
.PARAMETER JsonField
    JSON field name (when body is JSON) whose value must equal JsonValue.
.PARAMETER JsonValue
    Expected JSON field value (string compare Post-ConvertFromJson).
.PARAMETER YamlKey
    YAML key to search (with simple 'key: value' line search or numeric normalization fallback).
.PARAMETER YamlValue
    Expected YAML value for YamlKey.
.PARAMETER ReturnResponse
    Return the underlying Invoke-WebRequest result (for chaining) instead of nothing.
#>
function Assert-RouteContent {

    [CmdletBinding()] param(
        [Parameter(Mandatory)][string]$Uri,
        [ValidateSet('Get', 'Post', 'Put', 'Patch', 'Delete', 'Head')][string]$Method = 'Get',
        [int]$ExpectStatus = 200,
        [string]$Exact,
        [string]$Contains,
        [string]$Regex,
        [string]$JsonField,
        [string]$JsonValue,
        [string]$YamlKey,
        [string]$YamlValue,
        [hashtable]$Headers,
        [string]$ContentType,
        [object]$Body,
        [switch]$ReturnResponse
    )
    if (-not ($Exact -or $Contains -or $Regex -or ($JsonField -and $JsonValue) -or ($YamlKey -and $YamlValue))) {
        throw 'Assert-RouteContent: Provide one of Exact/Contains/Regex or JsonField+JsonValue or YamlKey+YamlValue.'
    }
    $invokeParams = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true; TimeoutSec = 10 }
    if ($Uri -like 'https://*') { $invokeParams.SkipCertificateCheck = $true }
    if ($Headers) { $invokeParams.Headers = $Headers }
    if ($Body) { $invokeParams.Body = $Body }
    if ($ContentType) { $invokeParams.ContentType = $ContentType }
    $resp = Invoke-WebRequest @invokeParams
    $resp.StatusCode | Should -Be $ExpectStatus
    $text = $resp.Content

    if ($Exact) { $text | Should -Be $Exact }
    if ($Contains) { ($text -like "*${Contains}*") | Should -BeTrue -Because "Body should contain '${Contains}'" }
    if ($Regex) { $text | Should -Match $Regex }
    if ($JsonField -and $JsonValue) { Assert-JsonFieldValue -Json $text -Field $JsonField -Expected $JsonValue }
    if ($YamlKey -and $YamlValue) { Assert-YamlContainsKeyValue -Yaml $text -Key $YamlKey -Expected $YamlValue }

    if ($ReturnResponse) { return $resp }
}

<#
.SYNOPSIS
    Normalize ISO 8601 instant strings in JSON to have exactly 7 fractional second digits.
.DESCRIPTION
    Finds all occurrences of ISO 8601 instant strings in the input string and normalizes
    their fractional seconds to exactly 7 digits by padding with zeros or truncating as needed.
    This ensures consistent representation for comparison purposes.
.PARAMETER StringToNormalize
    The input string potentially containing ISO 8601 instant strings.
.OUTPUTS
    The input string with normalized ISO 8601 instant strings.
#>
function Format-IsoInstant {
    param([string]$StringToNormalize)

    # Match ISO 8601 instants with optional fractional seconds and a TZ
    $pattern = '(?<base>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(?<frac>\d{1,7}))?(?<tz>Z|[+-]\d{2}:\d{2})'

    return [regex]::Replace($StringToNormalize, $pattern, {
            param($m) # <-- use the Match object, not $Matches
            $base = $m.Groups['base'].Value
            $tz = $m.Groups['tz'].Value
            $frac = if ($m.Groups['frac'].Success) {
                $m.Groups['frac'].Value.PadRight(7, '0').Substring(0, 7)
            } else {
                '0000000'
            }
            "$base.$frac$tz"
        })
}


<#
.SYNOPSIS
    Compare two objects deeply by converting them to JSON and comparing the strings.
.DESCRIPTION
    This function takes two objects, converts them to JSON with a depth of 100, and compares the resulting JSON strings.
    It is useful for deep comparison of complex objects in tests.
.PARAMETER Expected
    The expected object.
.PARAMETER Actual
    The actual object to compare against the expected.
.EXAMPLE
    $obj1 = @{ Key1 = "Value1"; Key2 = @{ SubKey = "SubValue" } }
    $obj2 = @{ Key1 = "Value1"; Key2 = @{ SubKey = "SubValue" } }
    Compare-Deep -Expected $obj1 -Actual $obj2
    # This will pass as the objects are deeply equivalent.
#>
function Compare-Deep {
    param(
        [Parameter()][AllowNull()]$Expected,
        [Parameter()][AllowNull()]$Actual
    )

    $expectedJson = ($Expected | ConvertTo-Json -Depth 99 -Compress).Replace("`r`n", "`n").Replace('\r\n', '\n')
    $actualJson = ($Actual | ConvertTo-Json -Depth 99 -Compress).Replace("`r`n", "`n").Replace('\r\n', '\n')

    $actualJson = Format-IsoInstant $actualJson
    $expectedJson = Format-IsoInstant $expectedJson

    $actualJson | Should -BeExactly $expectedJson
}

<#
.SYNOPSIS
  Compares two strings while normalizing line endings.

.DESCRIPTION
  This function trims both input strings and replaces all variations of line endings (`CRLF`, `LF`, `CR`) with a normalized `LF` (`\n`).
  It then compares the normalized strings for equality.

.PARAMETER InputString1
  The first string to compare.

.PARAMETER InputString2
  The second string to compare.

.OUTPUTS
  [bool]
  Returns `$true` if both strings are equal after normalization; otherwise, returns `$false`.

.EXAMPLE
  Compare-StringRnLn -InputString1 "Hello`r`nWorld" -InputString2 "Hello`nWorld"
  # Returns: $true

.EXAMPLE
  Compare-StringRnLn -InputString1 "Line1`r`nLine2" -InputString2 "Line1`rLine2"
  # Returns: $true

.NOTES
  This function ensures that strings with different line-ending formats are treated as equal if their content is otherwise identical.
#>
function Compare-StringRnLn {
    param (
        [string]$InputString1,
        [string]$InputString2
    )
    return ($InputString1.Trim() -replace "`r`n|`n|`r", "`n") -eq ($InputString2.Trim() -replace "`r`n|`n|`r", "`n")
}

<#
.SYNOPSIS
  Converts a PSCustomObject into an ordered hashtable.

.DESCRIPTION
  This function recursively converts a PSCustomObject, including nested objects and collections, into an ordered hashtable.
  It ensures that all properties are retained while maintaining their original structure.

.PARAMETER InputObject
  The PSCustomObject to be converted into an ordered hashtable.

.OUTPUTS
  [System.Collections.Specialized.OrderedDictionary]
  Returns an ordered hashtable representation of the input PSCustomObject.

.EXAMPLE
  $object = [PSCustomObject]@{ Name = "Pode"; Version = "2.0"; Config = [PSCustomObject]@{ Debug = $true } }
  Convert-PsCustomObjectToOrderedHashtable -InputObject $object
  # Returns: An ordered hashtable representation of $object.

.EXAMPLE
  $object = [PSCustomObject]@{ Users = @([PSCustomObject]@{ Name = "Alice" }, [PSCustomObject]@{ Name = "Bob" }) }
  Convert-PsCustomObjectToOrderedHashtable -InputObject $object
  # Returns: An ordered hashtable where 'Users' is an array of ordered hashtables.

.NOTES
  This function preserves key order and supports recursive conversion of nested objects and collections.
#>
function Convert-PsCustomObjectToOrderedHashtable {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [PSCustomObject]$InputObject
    )
    begin {
        <#
            .SYNOPSIS
                Converts a PSCustomObject to an ordered hashtable.
            .DESCRIPTION
                This function recursively converts a PSCustomObject, including nested objects and collections, into an ordered hashtable.
                It ensures that all properties are retained while maintaining their original structure.
            .PARAMETER InputObject
                The PSCustomObject to be converted into an ordered hashtable.
            .OUTPUTS
                [System.Collections.Specialized.OrderedDictionary]
                Returns an ordered hashtable representation of the input PSCustomObject.
            .EXAMPLE
                $object = [PSCustomObject]@{ Name = "Pode"; Version = "2.0"; Config = [PSCustomObject]@{ Debug = $true } }
                Convert-PsCustomObjectToOrderedHashtable -InputObject $object
                # Returns: An ordered hashtable representation of $object.
            .EXAMPLE
                $object = [PSCustomObject]@{ Users = @([PSCustomObject]@{ Name = "Alice" }, [PSCustomObject]@{ Name = "Bob" }) }
                Convert-PsCustomObjectToOrderedHashtable -InputObject $object
                # Returns: An ordered hashtable where 'Users' is an array of ordered hashtables.
            .NOTES
                This function preserves key order and supports recursive conversion of nested objects and collections.
        #>
        function Convert-ObjectRecursively {
            param (
                [Parameter(Mandatory = $true)]
                [System.Object]
                $InputObject
            )

            # Initialize an ordered dictionary
            $orderedHashtable = [ordered]@{}

            # Loop through each property of the PSCustomObject
            foreach ($property in $InputObject.PSObject.Properties) {
                # Check if the property value is a PSCustomObject
                if ($property.Value -is [PSCustomObject]) {
                    # Recursively convert the nested PSCustomObject
                    $orderedHashtable[$property.Name] = Convert-ObjectRecursively -InputObject $property.Value
                } elseif ($property.Value -is [System.Collections.IEnumerable] -and -not ($property.Value -is [string])) {
                    # If the value is a collection, check each element
                    $convertedCollection = @()
                    foreach ($item in $property.Value) {
                        if ($item -is [PSCustomObject]) {
                            $convertedCollection += Convert-ObjectRecursively -InputObject $item
                        } else {
                            $convertedCollection += $item
                        }
                    }
                    $orderedHashtable[$property.Name] = $convertedCollection
                } else {
                    # Add the property name and value to the ordered hashtable
                    $orderedHashtable[$property.Name] = $property.Value
                }
            }

            # Return the resulting ordered hashtable
            return $orderedHashtable
        }
    }
    process {
        # Call the recursive helper function for each input object
        Convert-ObjectRecursively -InputObject $InputObject
    }
}

<#
.SYNOPSIS
  Compares two hashtables to determine if they are equal.

.DESCRIPTION
  This function recursively compares two hashtables, checking whether they contain the same keys and values.
  It also handles nested hashtables and arrays, ensuring deep comparison of all elements.

.PARAMETER Hashtable1
  The first hashtable to compare.

.PARAMETER Hashtable2
  The second hashtable to compare.

.OUTPUTS
  [bool]
  Returns `$true` if both hashtables are equal, otherwise returns `$false`.

.EXAMPLE
  $hash1 = @{ Name = "Pode"; Version = "2.0"; Config = @{ Debug = $true } }
  $hash2 = @{ Name = "Pode"; Version = "2.0"; Config = @{ Debug = $true } }
  Compare-Hashtable -Hashtable1 $hash1 -Hashtable2 $hash2
  # Returns: $true

.EXAMPLE
  $hash1 = @{ Name = "Pode"; Version = "2.0" }
  $hash2 = @{ Name = "Pode"; Version = "2.1" }
  Compare-Hashtable -Hashtable1 $hash1 -Hashtable2 $hash2
  # Returns: $false

#>
function Compare-Hashtable {
    param (
        [object]$Hashtable1,
        [object]$Hashtable2
    )
    <#
        .SYNOPSIS
            Compares two values for equality.
        .DESCRIPTION
            This function checks if two values are equal, handling hashtables, arrays, and primitive types.
        .PARAMETER Value1
            The first value to compare.
        .PARAMETER Value2
            The second value to compare.
        .OUTPUTS
            [bool]
            Returns `$true` if the values are equal, otherwise returns `$false`.
    #>
    function Compare-Value($value1, $value2) {
        # Check if both values are hashtables
        if ((($value1 -is [hashtable] -or $value1 -is [System.Collections.Specialized.OrderedDictionary]) -and
                ($value2 -is [hashtable] -or $value2 -is [System.Collections.Specialized.OrderedDictionary]))) {
            return Compare-Hashtable -Hashtable1 $value1 -Hashtable2 $value2
        }
        # Check if both values are arrays
        elseif (($value1 -is [Object[]]) -and ($value2 -is [Object[]])) {
            if ($value1.Count -ne $value2.Count) {
                return $false
            }
            for ($i = 0; $i -lt $value1.Count; $i++) {
                $found = $false
                for ($j = 0; $j -lt $value2.Count; $j++) {
                    if ( Compare-Value $value1[$i] $value2[$j]) {
                        $found = $true
                    }
                }
                if ($found -eq $false) {
                    return $false
                }
            }
            return $true
        } else {
            if ($value1 -is [string] -and $value2 -is [string]) {
                return  Compare-StringRnLn $value1 $value2
            }
            # Check if the values are equal
            return $value1 -eq $value2
        }
    }

    $keys1 = $Hashtable1.Keys
    $keys2 = $Hashtable2.Keys

    # Check if both hashtables have the same keys
    if ($keys1.Count -ne $keys2.Count) {
        return $false
    }

    foreach ($key in $keys1) {
        if (! ($Hashtable2.Keys -contains $key)) {
            return $false
        }

        if ($Hashtable2[$key] -is [hashtable] -or $Hashtable2[$key] -is [System.Collections.Specialized.OrderedDictionary]) {
            if (! (Compare-Hashtable -Hashtable1 $Hashtable1[$key] -Hashtable2 $Hashtable2[$key])) {
                return $false
            }
        } elseif (!(Compare-Value $Hashtable1[$key] $Hashtable2[$key])) {
            return $false
        }
    }

    return $true
}



<#
.SYNOPSIS
    Retrieves Server-Sent Events (SSE) from a target server.

.DESCRIPTION
    The `Get-SseEvent` function connects to a server's SSE endpoint and streams incoming events.
    It first queries a metadata endpoint (default = `/sse`) to discover the actual SSE stream URL.
    Then it opens an HTTP/1.1 stream to avoid known flush issues with HTTP/2 and reads
    event frames from the stream, returning them as an array of objects.

.PARAMETER BaseUrl
    The base URL of the server hosting the SSE endpoint.
    Example: 'http://localhost:8080'

.PARAMETER MetaEndpoint
    The relative endpoint used to discover the SSE URL.
    Defaults to '/sse'.

.OUTPUTS
    [pscustomobject[]]
    An array of objects with properties:
    - Event: the event name (default is 'message')
    - Data:  the event payload

.EXAMPLE
    $events = Get-SseEvent -BaseUrl 'http://localhost:8080'

.EXAMPLE
    $events = Get-SseEvent -BaseUrl 'http://localhost:8080' -MetaEndpoint '/my_custom_sse'

.NOTES
    This function uses HttpClient and requires .NET 5+ / PowerShell 7+ for full compatibility.
    For internal or test use; not intended as a fully resilient production SSE client.

#>
function Get-SseEvent {
    param(
        [string]$BaseUrl,
        [string]$MetaEndpoint = '/sse'
    )
    # 1. One client, shared cookies
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseCookies = $true
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [timespan]::FromMinutes(10)      # infinite-ish

    # 2. Discover stream URL  (GET /sse returns  { Sse = @{ Url = '/sse_events' } })
    $metaRaw = $client.GetStringAsync("$BaseUrl$MetaEndpoint").Result
    $meta = $metaRaw | ConvertFrom-Json
    # Be flexible with casing: Sse/url vs sse/url
    $metaSse = $meta.Sse
    if (-not $metaSse -and $meta.sse) { $metaSse = $meta.sse }
    $metaUrl = $metaSse.Url
    if (-not $metaUrl -and $metaSse.url) { $metaUrl = $metaSse.url }
    $sseUri = $metaUrl
    if ([System.Uri]::IsWellFormedUriString($metaUrl, 'Absolute')) {
        $sseUri = $metaUrl
    } else {
        # Ensure the base ends with a slash, then combine
        $base = if ($BaseUrl.EndsWith('/')) { $BaseUrl } else { "$BaseUrl/" }
        $sseUri = [System.Uri]::new($base + ($metaUrl ?? '').TrimStart('/'))
    }


    # 3. Open the stream (HTTP/1.1 avoids rare HTTP/2 flush issues)
    $req = [System.Net.Http.HttpRequestMessage]::new('GET', $sseUri)
    $req.Version = [Version]::new(1, 1)
    $req.VersionPolicy = [System.Net.Http.HttpVersionPolicy]::RequestVersionExact
    $req.Headers.Accept.Add([System.Net.Http.Headers.MediaTypeWithQualityHeaderValue]::new('text/event-stream'))
    $resp = $client.SendAsync($req, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).Result
    $reader = [System.IO.StreamReader]::new($resp.Content.ReadAsStreamAsync().Result)

    # 4. Parse frames (default = "message")
    $events = @()
    $evtName = 'message'
    $evtData = ''
    while ($true) {
        $line = $reader.ReadLine(); if ($null -eq $line) { break }
        if ($line -eq '') {
            if ($evtData) {
                $events += [pscustomobject]@{Event = $evtName; Data = $evtData }
                $evtName = 'message'; $evtData = ''
            }
            continue
        }
        if ($line.StartsWith('event:')) { $evtName = $line.Substring(6).Trim() }
        elseif ($line.StartsWith('data:')) {
            if ($evtData) {
                $evtData += "`n"
            }
            $evtData += $line.Substring(5).Trim()
        }
    }

    try { $reader.Dispose() } catch { Write-Warning ("Failed to dispose SSE reader: $($_.Exception.Message)") }
    try { $resp.Dispose() } catch { Write-Warning ("Failed to dispose SSE response: $($_.Exception.Message)") }
    try { $client.Dispose() } catch { Write-Warning ("Failed to dispose SSE client: $($_.Exception.Message)") }
    return $events
}

<#
.SYNOPSIS
    Connects to a SignalR hub and captures messages.
.DESCRIPTION
    This function connects to a SignalR hub using WebSockets, performs the necessary handshake,
    and listens for incoming messages. It collects messages up to a specified count or until a timeout
    occurs. Optionally, it can filter messages by target method names and invoke a callback after
    the connection is established.
.PARAMETER BaseUrl
    The base URL of the server hosting the SignalR hub (e.g., 'http://localhost:5000').
.PARAMETER HubPath
    The relative path to the SignalR hub (default is '/hubs/kestrun').
.PARAMETER Count
    The maximum number of messages to collect (default is 5).
.PARAMETER TimeoutSeconds
    The overall timeout in seconds for the operation (default is 30).
.PARAMETER Targets
    An array of target method names to filter messages (e.g., 'ReceiveEvent'). If specified,
    only messages with these target names will be collected.
.PARAMETER OnConnected
    An optional script block to invoke after the connection is established and the handshake is complete,
    but before entering the message receive loop. This can be used to send initial messages or perform
    other setup tasks.
.PARAMETER OnConnectedArg
    An optional argument to pass to the OnConnected script block.
.OUTPUTS
    An array of objects representing the received messages, each with properties:
    - InvocationId: The invocation ID of the message (if present).
    - Target: The target method name of the message.
    - Arguments: The arguments of the message.
    - Raw: The raw JSON string of the message.
.EXAMPLE
    $messages = Get-SignalRMessage -BaseUrl 'http://localhost:5000' -HubPath '/hubs/kestrun' -Count 10 -TimeoutSeconds 60
    # Connects to the specified SignalR hub and collects up to 10 messages or until 60 seconds elapse.
.EXAMPLE
    $messages = Get-SignalRMessage -BaseUrl 'http://localhost:5000' -Targets 'ReceiveEvent' -OnConnected {
        # Send an initial message or perform setup
        $initMessage = '{"type":1,"target":"JoinGroup","arguments":["TestGroup"]}' + [char]0x1e
        $buf = [System.Text.Encoding]::UTF8.GetBytes($initMessage)
        $seg = [ArraySegment[byte]]::new($buf)
        $arg.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait()
    }
    # Connects to the hub, invokes the OnConnected script block to join a group,
    # and collects messages targeting 'ReceiveEvent'.
.NOTES
    This function requires .NET 5+ / PowerShell 7+ for full compatibility.
    It is intended for internal or test use and may not handle all edge cases of the SignalR protocol.
#>
function Get-SignalRMessage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$BaseUrl,           # e.g., http://localhost:5000
        [string]$HubPath = '/hubs/kestrun',                 # default hub path
        [int]$Count = 5,                                    # max messages to collect
        [int]$TimeoutSeconds = 30,                          # overall timeout
        [string[]]$Targets,                                 # filter by hub method names (e.g., 'ReceiveEvent')
        [scriptblock]$OnConnected,                          # optional callback invoked after handshake, before receive loop
        [object]$OnConnectedArg                             # optional argument passed to OnConnected
    )

    <#
    .SYNOPSIS
        Retrieves a property value from an object or hashtable, case-insensitively.
    .DESCRIPTION
        This helper function extracts a property value from a given object or hashtable,
        performing a case-insensitive search for the property name.
    .PARAMETER obj
        The object or hashtable from which to retrieve the property value.
    .PARAMETER name
        The name of the property to retrieve (case-insensitive).
    .OUTPUTS
        The value of the specified property, or $null if not found.
    #>
    function Get-PropValue($obj, [string]$name) {

        if ($null -eq $obj) { return $null }
        if ($obj -is [hashtable]) {
            $key = $obj.Keys | Where-Object { $_ -is [string] -and $_.ToString() -ieq $name } | Select-Object -First 1
            if ($null -ne $key) { return $obj[$key] }
            return $null
        }
        $prop = $obj.PSObject.Properties | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
        if ($prop) { return $prop.Value }
        return $null
    }

    # Build negotiate URL
    $negotiateUrl = "$BaseUrl$HubPath/negotiate?negotiateVersion=1"
    $http = [System.Net.Http.HttpClient]::new()
    try {
        $negRaw = $http.PostAsync($negotiateUrl, [System.Net.Http.StringContent]::new('')).Result.Content.ReadAsStringAsync().Result
    } catch {
        throw ('Negotiate failed for {0}: {1}' -f $negotiateUrl, $_.Exception.Message)
    } finally {
        try { $http.Dispose() } catch { Write-Warning ('Failed to dispose HttpClient: {0}' -f $_.Exception.Message) }
    }
    $neg = $negRaw | ConvertFrom-Json
    # Prefer connectionToken when present; fall back to connectionId
    $connToken = Get-PropValue $neg 'connectionToken'
    if (-not $connToken) { $connToken = Get-PropValue $neg 'ConnectionToken' }
    if (-not $connToken) { $connToken = Get-PropValue $neg 'connectionId' }
    if (-not $connToken) { $connToken = Get-PropValue $neg 'ConnectionId' }
    if (-not $connToken) { throw "Negotiate did not return connectionToken/connectionId. Payload: $negRaw" }

    # Optional redirect URL and access token
    $redirectUrl = Get-PropValue $neg 'url'
    if (-not $redirectUrl) { $redirectUrl = Get-PropValue $neg 'Url' }
    $accessToken = Get-PropValue $neg 'accessToken'
    if (-not $accessToken) { $accessToken = Get-PropValue $neg 'AccessToken' }

    # Build WebSocket URL
    if ($redirectUrl) {
        # Replace http/https with ws/wss
        if ($redirectUrl -like 'http*') {
            $wsUri = $redirectUrl -replace '^https', 'wss' -replace '^http', 'ws'
        } else {
            # Relative redirect URL; resolve against BaseUrl
            $baseUri = [System.Uri]$BaseUrl
            $combined = [System.Uri]::new($baseUri, $redirectUrl)
            $wsUri = $combined.AbsoluteUri -replace '^https', 'wss' -replace '^http', 'ws'
        }
        # If negotiate url already contains id or other params, append if missing
        if ($wsUri -notmatch '[?&]id=') { $wsUri = ('{0}{1}id={2}' -f $wsUri, ($wsUri -match '\?' ? '&' : '?'), $connToken) }
    } else {
        $uri = [System.Uri]$BaseUrl
        $wsScheme = if ($uri.Scheme -eq 'https') { 'wss' } else { 'ws' }
        $wsUri = '{0}://{1}{2}?id={3}' -f $wsScheme, $uri.Authority, $HubPath, $connToken
    }

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $cts = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
    $encoding = [System.Text.Encoding]::UTF8
    $recordSep = [char]0x1e

    try {
        if ($accessToken) {
            $socket.Options.SetRequestHeader('Authorization', 'Bearer ' + $accessToken)
        }
        $socket.ConnectAsync([System.Uri]$wsUri, $cts.Token).Wait()

        # Handshake: {"protocol":"json","version":1}\x1e
        $handshake = '{"protocol":"json","version":1}' + $recordSep
        $hsBuf = $encoding.GetBytes($handshake)
        $handshakeSeg = [ArraySegment[byte]]::new($hsBuf)
        $socket.SendAsync($handshakeSeg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait()

        # Allow caller to trigger HTTP routes or hub actions once connected
        if ($OnConnected) {
            try {
                if ($PSBoundParameters.ContainsKey('OnConnectedArg')) { & $OnConnected $OnConnectedArg }
                else { & $OnConnected }
            } catch { Write-Warning ('OnConnected callback failed: {0}' -f $_.Exception.Message) }
        }

        $messages = @()
        $buffer = New-Object byte[] 8192
        $sb = [System.Text.StringBuilder]::new()

        $targetFilter = if ($null -eq $Targets) { @() } else { @($Targets) }
        $hasFilter = ($targetFilter -is [array] -and $targetFilter.Length -gt 0)
        while ($messages.Count -lt $Count -and $socket.State -eq [System.Net.WebSockets.WebSocketState]::Open -and -not $cts.IsCancellationRequested) {
            [System.Net.WebSockets.WebSocketReceiveResult] $recvResult = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $cts.Token).Result

            $bytes = 0
            try {
                $bytes = [int]($recvResult.Count)
            } catch {
                # Fallback: if property not available, skip processing this iteration
                continue
            }

            if ($bytes -le 0) { continue }
            $chunk = $encoding.GetString($buffer, 0, $bytes)
            $sb.Append($chunk) | Out-Null

            # Messages are delimited by 0x1E
            $text = $sb.ToString()
            $parts = $text.Split($recordSep)
            # Keep the last (possibly incomplete) segment in the StringBuilder
            if ($parts.Length -gt 0) {
                $sb.Clear() | Out-Null
                $sb.Append($parts[-1]) | Out-Null
            }

            for ($i = 0; $i -lt $parts.Length - 1; $i++) {
                $msg = $parts[$i]
                if (-not $msg) { continue }
                try {
                    $json = $msg | ConvertFrom-Json
                } catch {
                    continue
                }

                # SignalR handshake returns {} — ignore. We only want invocation messages (type == 1)
                $type = Get-PropValue $json 'type'
                if ($type -ne 1) { continue }

                $target = Get-PropValue $json 'target'
                $invArgs = Get-PropValue $json 'arguments'
                if ($hasFilter -and -not ($targetFilter -contains $target)) { continue }

                $messages += [pscustomobject]@{
                    Target = $target
                    Arguments = $invArgs
                    Raw = $msg
                }

                if ($messages.Count -ge $Count) { break }
            }
        }

        return $messages
    } finally {
        try {
            if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                $socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'done', $cts.Token).Wait()
            }
        } catch { Write-Warning ("Failed to close SignalR socket: $($_.Exception.Message)") }
        try { $socket.Dispose() } catch { Write-Warning ("Failed to dispose SignalR socket: $($_.Exception.Message)") }
        try { $cts.Dispose() } catch { Write-Warning ("Failed to dispose SignalR CTS: $($_.Exception.Message)") }
    }
}

<#
.SYNOPSIS
  Minimal, curl-backed replacement for Invoke-WebRequest that can
  stream very large responses (≫2 GB) on Windows, Linux, and macOS.
  Now supports range-based downloads for very large files.

.PARAMETER Uri
  URL to fetch.

.PARAMETER OutFile
  Path where the response body should be written.
  If omitted the body is buffered into the returned object
  (fine for your text tests, but skip this for multi-GB payloads).
.PARAMETER Headers
  Hashtable of request headers.
.PARAMETER PassThru
  Return a response object instead of being silent.
.PARAMETER UseRangeDownload
  Use range-based downloading for large files. Automatically detects file size
  and downloads in chunks, then joins them together.
.PARAMETER RangeSize
  Size of each range chunk when UseRangeDownload is enabled. Default is 1GB.
.PARAMETER DownloadDir
  Directory for temporary part files when using range downloads.
  If not specified, uses the OutFile directory or a temporary directory.
.PARAMETER ETag
  ETag value to use for conditional requests. If provided, the request will include
  an `If-None-Match` header with this value.
.PARAMETER IfModifiedSince
  DateTime value for the `If-Modified-Since` header.
  If provided, the request will include this header to check for modifications.
.PARAMETER AcceptEncoding
  Accept-Encoding header value (e.g., 'gzip', 'deflate', 'br').
  If specified, adds an `Accept-Encoding` header to the request.
.PARAMETER SkipCertificateCheck
  Skip TLS certificate validation (useful for self-signed certs in tests).
.EXAMPLE
  # Simple GET with response body returned
  $resp = Invoke-CurlRequest -Url 'https://httpbin.org/get' -PassThru
  $resp.StatusCode  # 200
  $resp.Headers    # Response headers
  $resp.Content    # Response body
.EXAMPLE
  # Download a file to disk
  Invoke-CurlRequest -Url 'https://example.com/largefile.zip' -OutFile 'largefile.zip'
.EXAMPLE
  # Download a file using range requests (for very large files)
  Invoke-CurlRequest -Url 'https://example.com/verylargefile.iso' -OutFile 'verylargefile.iso' -UseRangeDownload -RangeSize 512MB
.EXAMPLE
  # Conditional GET using ETag  (returns 304 if not modified)
    $resp = Invoke-CurlRequest -Url 'https://example.com/resource' -ETag 'W/"123456789"' -PassThru
    $resp.StatusCode  # 200 or 304
.EXAMPLE
  # Conditional GET using If-Modified-Since
    $resp = Invoke-CurlRequest -Url 'https://example.com/resource' -IfModifiedSince (Get-Date '2024-01-01T00:00:00Z') -PassThru
    $resp.StatusCode  # 200 or 304
.NOTES
  This function requires curl to be installed and available in the system PATH.
  It is designed to work cross-platform (Windows, Linux, macOS).
  For very large file downloads, use the -UseRangeDownload parameter to avoid
  memory issues and improve reliability.
#>
function Invoke-CurlRequest {

    [CmdletBinding(DefaultParameterSetName = 'Default')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]
        $Url,

        [Parameter()]
        [string]
        $OutFile,

        [Parameter()]
        [hashtable]
        $Headers,

        [Parameter()]
        [switch]
        $PassThru,

        [Parameter()]
        [string]
        $DownloadDir,

        [Parameter(ParameterSetName = 'Default')]
        [Parameter(ParameterSetName = 'Etag')]
        [Parameter(ParameterSetName = 'IfModifiedSince')]
        [ValidateSet('gzip', 'deflate', 'br')]
        [string]
        $AcceptEncoding,

        [Parameter(Mandatory = $true, ParameterSetName = 'RangeDownload')]
        [switch]
        $UseRangeDownload,

        [Parameter( ParameterSetName = 'RangeDownload')]
        [long]
        $RangeSize = 1GB,

        [Parameter(Mandatory = $true, ParameterSetName = 'Etag')]
        [string]
        $ETag,

        [Parameter(Mandatory = $true, ParameterSetName = 'IfModifiedSince')]
        [datetime]
        $IfModifiedSince,

        [Parameter()]
        [switch]
        $SkipCertificateCheck

    )

    # ------------------------------------------------------------
    # Handle range downloads
    # ------------------------------------------------------------
    if ($UseRangeDownload) {
        # Locate the real curl binary (cross-platform, bypass alias)
        if ($PSEdition -eq 'Desktop' -or $IsWindows) { $curlCmd = 'curl.exe' } else { $curlCmd = 'curl' }

        # First get the content length with a HEAD request
        $tmpHdr = [IO.Path]::GetTempFileName()
        $headArgs = @(
            '--silent', '--show-error',
            '--location',
            '--head', # HEAD request only
            '--dump-header', $tmpHdr,
            '--write-out', '%{http_code}',
            '--url', $Url
        )

        $statusLine = & $curlCmd @headArgs
        if ($LASTEXITCODE) {
            throw "curl HEAD request failed with code $LASTEXITCODE"
        }

        # Parse headers from HEAD response
        $hdrHash = @{}
        foreach ($line in Get-Content $tmpHdr) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            if ($line -match '^(?<k>[^:]+):\s*(?<v>.+)$') {
                $hdrHash[$matches.k.Trim()] = $matches.v.Trim()
            }
        }
        Remove-Item $tmpHdr -Force

        if (-not $hdrHash.ContainsKey('Content-Length')) {
            throw 'Server does not provide Content-Length header, cannot use range downloads'
        }

        $length = [int64]$hdrHash['Content-Length']
        if (-not $DownloadDir) {
            if ($OutFile) {
                $DownloadDir = Split-Path -Path $OutFile -Parent
            } else {
                $DownloadDir = [IO.Path]::GetTempPath()
            }
        }

        # Create download directory if it doesn't exist
        if (-not (Test-Path -Path $DownloadDir)) {
            New-Item -Path $DownloadDir -ItemType Directory -Force | Out-Null
        }

        # Calculate parts and download each range
        $parts = 0..[math]::Floor(($length - 1) / $RangeSize) | ForEach-Object {
            $start = $_ * $RangeSize
            $end = [math]::Min($length - 1, $start + $RangeSize - 1)
            $part = Join-Path -Path $DownloadDir -ChildPath "part$_.bin"

            # Download this range using curl directly (avoid recursion)
            $rangeArgs = @(
                '--silent', '--show-error',
                '--location',
                '--output', $part,
                '-H', "Range: bytes=$start-$end",
                '--url', $Url
            )

            # Add any additional headers
            if ($Headers) {
                foreach ($k in $Headers.Keys) {
                    $rangeArgs += @('-H', "$($k): $($Headers[$k])")
                }
            }

            & $curlCmd @rangeArgs
            if ($LASTEXITCODE) {
                throw "curl range request failed with code $LASTEXITCODE"
            }
            $part
        }

        # Join all parts into final file
        $joined = if ($OutFile) { $OutFile } else { Join-Path -Path $DownloadDir -ChildPath 'joined.tmp' }
        $out = [System.IO.File]::Create($joined)
        try {
            foreach ($p in $parts) {
                $bytes = [System.IO.File]::ReadAllBytes($p)
                $out.Write($bytes, 0, $bytes.Length)
                Remove-Item $p -Force
            }
        } finally {
            $out.Dispose()
        }

        if ($PassThru) {
            return [PSCustomObject]@{
                StatusCode = 200
                Headers = $hdrHash
                OutFile = $joined
            }
        }
        return
    }

    # ------------------------------------------------------------
    # Normal (non-range) download logic
    # ------------------------------------------------------------

    # Locate the real curl binary (cross-platform, bypass alias)
    if ($PSEdition -eq 'Desktop' -or $IsWindows) { $curlCmd = 'curl.exe' } else { $curlCmd = 'curl' }
    # ------------------------------------------------------------
    # Prep temporary files
    # ------------------------------------------------------------
    $tmpHdr = [IO.Path]::GetTempFileName()
    $tmpBody = if ($OutFile) { $OutFile } else { [IO.Path]::GetTempFileName() }

    # ------------------------------------------------------------
    # Build argument list
    # ------------------------------------------------------------
    $arguments = @(
        '--silent', '--show-error', # quiet transfer, still show errors
        '--location', # follow 3xx
        '--dump-header', $tmpHdr, # capture headers
        '--output', $tmpBody, # stream body
        '--write-out', '%{http_code}'    # print status at the end
    )

    if ($SkipCertificateCheck) {
        $arguments += '--insecure'  # skip TLS cert validation
    }
    # If Accept-Encoding is specified, add it to the request and use --compressed.
    # curl will automatically handle decompression only if --compressed is used (which happens when Accept-Encoding is set).
    if ($AcceptEncoding) {
        if ($null -eq $Headers) {
            $Headers = @{}
        }
        $Headers['Accept-Encoding'] = $AcceptEncoding
        $arguments += @('--compressed')  # curl will handle Accept-Encoding
    }

    # if Etag header is set, we will add it to the request.
    # This is used for conditional requests.
    if ($ETag) {
        if ($PSEdition -eq 'Desktop' ) {
            $arguments += @('-H', "If-None-Match: ""$ETag""")
        } else {
            $arguments += @('-H', "If-None-Match: $ETag")
        }
    }

    # IfModifiedSince header
    # If the header is not set, we will not add it to the request.
    if ($IfModifiedSince) {
        $arguments += @('-H', "If-Modified-Since: $($IfModifiedSince.ToString('R'))")
    }

    # Add any additional headers
    if ($Headers) {
        foreach ($k in $Headers.Keys) {
            $arguments += @('-H', ('{0}: {1}' -f $k, $Headers[$k]))
        }
    }

    $arguments += '--url', $Url

    # ------------------------------------------------------------
    # Run curl
    # ------------------------------------------------------------
    if ($PSEdition -eq 'Desktop') {
        $statusLine = cmd /c $curlCmd @arguments
    } else {
        $statusLine = & $curlCmd @arguments
    }
    if ($LASTEXITCODE) {
        throw "curl exited with code $LASTEXITCODE"
    }
    $statusCode = [int]$statusLine

    # ------------------------------------------------------------
    # Parse headers
    # ------------------------------------------------------------
    $hdrHash = @{}
    foreach ($line in Get-Content $tmpHdr) {
        if ([string]::IsNullOrWhiteSpace($line)) { break }
        if ($line -match '^(?<k>[^:]+):\s*(?<v>.+)$') {
            $hdrHash[$matches.k.Trim()] = $matches.v.Trim()
        }
    }

    # Clean up temporary header file
    Remove-Item $tmpHdr -Force

    # ------------------------------------------------------------
    # Build response object (if requested)
    # ------------------------------------------------------------
    if ($PassThru) {
        $raw = if (-not $OutFile) { [IO.File]::ReadAllBytes($tmpBody) }
        $content = if ($raw) { [Text.Encoding]::UTF8.GetString($raw) }

        [PSCustomObject]@{
            StatusCode = $statusCode
            Headers = $hdrHash
            RawContent = $raw
            Content = $content
        }
    }

    # Clean up temporary body file if we created one
    if (-not $OutFile -and -not $PassThru) {
        Remove-Item $tmpBody -Force
    }
}

<#
.SYNOPSIS
    Helper function to find a header in a case-insensitive manner.
.DESCRIPTION
    This function searches for a header in a hashtable of headers, ignoring case.
.PARAMETER hdrs
    The hashtable of headers to search.
.PARAMETER name
    The name of the header to find.
.OUTPUTS
    The value of the header if found, otherwise $null.
#>
function Find-Header($hdrs, [string]$name) {
    foreach ($k in $hdrs.Keys) { if ($k -ieq $name) { return $hdrs[$k] } }
    return $null
}

<#
.SYNOPSIS
    Probe a route with and without gzip to capture size and encoding differences.
.DESCRIPTION
    Issues two requests to the provided relative path (one normal, one with 'Accept-Encoding: gzip')
    and returns an object containing raw/gzip Content-Encoding headers, lengths, and responses.
.PARAMETER Instance
    The running example instance object from Start-ExampleScript.
.PARAMETER Path
    The relative path (begin with '/') to probe.
.OUTPUTS
    PSCustomObject with properties: Path, RawEncoding, GzipEncoding, RawLength, GzipLength, Raw, Gz
#>
function Get-CompressionProbe {
    [CmdletBinding()] param(
        [Parameter(Mandatory)][object]$Instance,
        [Parameter(Mandatory)][string]$Path,
        [switch]$UseCurlFallback
    )
    if (-not $Path.StartsWith('/')) { $Path = '/' + $Path }
    $base = $Instance.Url
    $insecure = $base -like 'https://*'
    # Raw (no Accept-Encoding header)
    $raw = Get-HttpHeadersRaw -Uri "$base$Path" -IncludeBody -Insecure:$insecure -NoAcceptEncoding
    # Gzip
    $gz = Get-HttpHeadersRaw -Uri "$base$Path" -IncludeBody -Insecure:$insecure -AcceptEncoding 'gzip'


    $rawEncoding = Find-Header $raw.Headers 'Content-Encoding'
    $gzEncoding = Find-Header $gz.Headers 'Content-Encoding'
    $rawLen = if (Find-Header $raw.Headers 'Content-Length') { [int](Find-Header $raw.Headers 'Content-Length') } else { $raw.BodyLength }
    $gzLen = if (Find-Header $gz.Headers 'Content-Length') { [int](Find-Header $gz.Headers 'Content-Length') } else { $gz.BodyLength }

    # Optional curl fallback for edge cases
    if ($UseCurlFallback -and -not $gzEncoding) {
        try {
            $curlGz = Invoke-CurlRequest -Url "$base$Path" -AcceptEncoding gzip -PassThru -SkipCertificateCheck:$insecure
            if ($curlGz.Headers.ContainsKey('Content-Encoding')) { $gzEncoding = $curlGz.Headers['Content-Encoding'] }
            if ($curlGz.Headers.ContainsKey('Content-Length')) { $gzLen = [int]$curlGz.Headers['Content-Length'] } elseif ($curlGz.RawContent) { $gzLen = $curlGz.RawContent.Length }
        } catch { Write-Verbose "Curl fallback (gzip) failed: $($_.Exception.Message)" -Verbose }
    }

    [pscustomobject]@{
        Path = $Path
        RawEncoding = $rawEncoding
        GzipEncoding = $gzEncoding
        RawLength = $rawLen
        GzipLength = $gzLen
        Raw = $raw
        Gz = $gz
    }
}

<#
.SYNOPSIS
    Fetches raw HTTP headers from a specified URI using low-level sockets.
.DESCRIPTION
    This function connects to the specified URI using TCP sockets and retrieves the raw HTTP headers.
    It supports both HTTP and HTTPS schemes, with an option to skip TLS certificate validation.
.PARAMETER Uri
    The full URI to fetch headers from (e.g., "http://example.com/path").
.PARAMETER HostOverride
    Optional host header override (useful for virtual hosting scenarios).
.PARAMETER Insecure
    If specified, skips TLS certificate validation (useful for self-signed certificates).
.PARAMETER AsHashtable
    If specified, returns headers as an ordered hashtable instead of a raw string.
.OUTPUTS
    [string] or [hashtable]
    Returns the raw HTTP headers as a string or an ordered hashtable if AsHashtable is specified.
.NOTES
    This function uses low-level socket programming to fetch headers and does not rely on higher-level HTTP libraries.
#>
function Get-HttpHeadersRaw {
    [CmdletBinding()]
    [OutputType([string], [hashtable], [System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)]
        [string]$Uri,
        [string]$HostOverride,
        [switch]$Insecure,
        [switch]$AsHashtable,
        [switch]$IncludeBody,
        [string]$AcceptEncoding,
        [switch]$NoAcceptEncoding,
        [ValidateRange(1, 300)]
        [int]$TimeoutSeconds = $(if ($env:KR_TEST_RAW_HTTP_TIMEOUT_SECONDS) { [int]$env:KR_TEST_RAW_HTTP_TIMEOUT_SECONDS } else { 15 })
    )

    $u = [Uri]$Uri
    if ($u.Scheme -notin @('http', 'https')) {
        throw "Unsupported scheme '$($u.Scheme)'. Use http or https."
    }

    $targetHost = if ($HostOverride) { $HostOverride } else { $u.Host }
    $port = if ($u.IsDefaultPort) { if ($u.Scheme -eq 'https') { 443 } else { 80 } } else { $u.Port }
    $path = if ([string]::IsNullOrEmpty($u.PathAndQuery)) { '/' } else { $u.PathAndQuery }
    $crlf = "`r`n"
    $timeoutMs = [Math]::Max(1000, $TimeoutSeconds * 1000)

    $waitForTask = {
        param(
            [Parameter(Mandatory)]
            [System.Threading.Tasks.Task]$Task,
            [Parameter(Mandatory)]
            [string]$Operation
        )

        if (-not $Task.Wait($timeoutMs)) {
            throw [System.TimeoutException]::new("Timed out after ${TimeoutSeconds}s while ${Operation}: $Uri")
        }

        $result = $Task.GetAwaiter().GetResult()
        if ($null -eq $result) {
            return
        }

        if ($result.GetType().FullName -eq 'System.Threading.Tasks.VoidTaskResult') {
            return
        }

        return $result
    }

    $client = [System.Net.Sockets.TcpClient]::new()
    & $waitForTask $client.ConnectAsync($u.Host, $port) "connecting to $($u.Host):$port"
    $netStream = $client.GetStream()
    $stream = $null

    try {
        if ($u.Scheme -eq 'https') {
            $cb = if ($Insecure) {
                if (-not ('Kestrun.Testing.SslCallbacks' -as [type])) {
                    Add-Type -TypeDefinition @'
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Kestrun.Testing
{
    public static class SslCallbacks
    {
        public static bool AllowAll(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
'@
                }

                [System.Net.Security.RemoteCertificateValidationCallback] [Kestrun.Testing.SslCallbacks]::AllowAll
            } else { $null }

            $ssl = [System.Net.Security.SslStream]::new($netStream, $false, $cb)
            $proto = [System.Security.Authentication.SslProtocols]::Tls12 -bor `
                [System.Security.Authentication.SslProtocols]::Tls13
            # SNI uses targetHost (can be different from the IP/endpoint you connect to)
            & $waitForTask $ssl.AuthenticateAsClientAsync($targetHost, $null, $proto, $false) "performing TLS handshake with $targetHost"
            $stream = $ssl
        } else {
            $stream = $netStream
        }

        # Determine Accept-Encoding header logic
        $aeHeader = $null
        if (-not $NoAcceptEncoding) {
            if ($PSBoundParameters.ContainsKey('AcceptEncoding')) {
                if ($AcceptEncoding -and $AcceptEncoding.Trim().Length -gt 0) {
                    $aeHeader = "Accept-Encoding: $AcceptEncoding$crlf"
                } else {
                    # Explicit empty string means omit header
                    $aeHeader = ''
                }
            } else {
                # default list (broad) if not overridden
                $aeHeader = "Accept-Encoding: gzip, deflate, br$crlf"
            }
        } else {
            $aeHeader = ''
        }

        $request = "GET $path HTTP/1.1$crlf" +
        "Host: $targetHost$crlf" +
        ($aeHeader) +
        "Connection: close$crlf" +
        $crlf

        $utf8 = [Text.Encoding]::UTF8
        $reqBytes = [Text.Encoding]::ASCII.GetBytes($request)
        & $waitForTask $stream.WriteAsync($reqBytes, 0, $reqBytes.Length) "writing request to $Uri"

        # For header-only probes, stop reading as soon as the header terminator
        # arrives. Waiting for the server to close the connection can keep a
        # limited-connection example busy long enough to poison later probes.
        $buf = [byte[]]::new(8192)
        $ms = [System.IO.MemoryStream]::new()
        $headerTerminatorIndex = -1
        while (($n = & $waitForTask $stream.ReadAsync($buf, 0, $buf.Length) "reading response from $Uri") -gt 0) {
            $ms.Write($buf, 0, $n)
            if (-not $IncludeBody) {
                $current = $ms.ToArray()
                for ($i = 0; $i -le $current.Length - 4; $i++) {
                    if (($current[$i] -eq 13) -and ($current[$i + 1] -eq 10) -and ($current[$i + 2] -eq 13) -and ($current[$i + 3] -eq 10)) {
                        $headerTerminatorIndex = $i
                        break
                    }
                }

                if ($headerTerminatorIndex -ge 0) {
                    break
                }
            }
        }
        $all = $ms.ToArray()

        # Locate CRLFCRLF separator between headers and body
        $sep = $headerTerminatorIndex
        if ($sep -lt 0) {
            for ($i = 0; $i -le $all.Length - 4; $i++) {
                if (($all[$i] -eq 13) -and ($all[$i + 1] -eq 10) -and ($all[$i + 2] -eq 13) -and ($all[$i + 3] -eq 10)) {
                    $sep = $i
                    break
                }
            }
        }

        if ($sep -lt 0) {
            # No header terminator found; return raw text or a simple bag
            $rawText = $utf8.GetString($all)
            if ($AsHashtable -or $IncludeBody) {
                return [ordered]@{
                    'Status-Line' = '(unknown)'
                    'StatusCode' = $null
                    'Version' = $null
                    'Raw' = $rawText
                }
            } else {
                return $rawText
            }
        }

        $headerBytes = $all[0..($sep + 3)]
        $bodyBytes = if ($sep + 4 -lt $all.Length) { $all[($sep + 4)..($all.Length - 1)] } else { [byte[]]@() }
        $headerText = $utf8.GetString($headerBytes)

        if ($IncludeBody) {
            # Structured return: ordered Headers + raw Body bytes + StatusCode + BodyLength
            $headersOrdered = [ordered]@{}
            $lines = $headerText -split "`r`n"
            if ($lines.Length -gt 0) { $headersOrdered['Status-Line'] = $lines[0] }
            for ($j = 1; $j -lt $lines.Length; $j++) {
                $line = $lines[$j]
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $idx = $line.IndexOf(':')
                if ($idx -ge 0) {
                    $k = $line.Substring(0, $idx)
                    $v = $line.Substring($idx + 1).TrimStart()
                    if ($headersOrdered.Contains($k)) {
                        $existing = $headersOrdered[$k]
                        if ($existing -is [System.Collections.IList]) {
                            $existing.Add($v) | Out-Null
                        } else {
                            $headersOrdered[$k] = [System.Collections.ArrayList]@($existing, $v)
                        }
                    } else {
                        $headersOrdered[$k] = $v
                    }
                }
            }
            $statusCode = $null
            if ($headersOrdered['Status-Line'] -match 'HTTP/\d\.\d\s+(\d{3})') { $statusCode = [int]$matches[1] }
            return [pscustomobject]@{
                Headers = $headersOrdered
                Body = $bodyBytes
                StatusCode = $statusCode
                BodyLength = $bodyBytes.Length
            }
        }

        if ($AsHashtable) {
            $ht = [ordered]@{}
            $lines = $headerText -split "`r`n"
            if ($lines.Length -gt 0) {
                $ht['Status-Line'] = $lines[0]
                $ht['StatusCode'] = if ($lines[0] -match 'HTTP/\d\.\d\s+(\d{3})') { [int]$matches[1] } else { $null }
                $ht['Version'] = if ($lines[0] -match 'HTTP/(\d\.\d)') { $matches[1] } else { $null }
            }
            for ($j = 1; $j -lt $lines.Length; $j++) {
                $line = $lines[$j]
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $idx = $line.IndexOf(':')
                if ($idx -ge 0) {
                    $k = $line.Substring(0, $idx)
                    $v = $line.Substring($idx + 1).TrimStart()
                    if ($ht.Contains($k)) {
                        $existing = $ht[$k]
                        if ($existing -is [System.Collections.IList]) {
                            $existing.Add($v) | Out-Null
                        } else {
                            $ht[$k] = [System.Collections.ArrayList]@($existing, $v)
                        }
                    } else {
                        $ht[$k] = $v
                    }
                }
            }
            return $ht
        } else {
            return $headerText
        }
    } finally {
        if ($stream) { $stream.Dispose() }
        if ($netStream) { $netStream.Dispose() }
        if ($client) { $client.Dispose() }
    }
}



<#
.SYNOPSIS
    Attempt Brotli decompression if available.
.DESCRIPTION
    Uses System.IO.Compression.BrotliStream if available (requires .NET Core 2.1+ / .NET 5+).
    Brotli magic isn't fixed, so we use a heuristic: if gzip test fails and size > 20, try brotli.
    If BrotliStream type isn't available, this is a no-op.
.PARAMETER data
    Byte array to attempt to decompress.
.OUTPUTS
    Decompressed byte array if successful, otherwise $null.
.NOTES
    This function will not throw; it returns $null on failure.
#>
function ConvertFrom-BrotliCompression([byte[]]$data) {
    try {
        $brotliType = [type]::GetType('System.IO.Compression.BrotliStream, System.IO.Compression.Brotli')
        if (-not $brotliType) { return $null }
        # Brotli magic isn't fixed like gzip; quick attempt only if not gzip
        if ($data.Length -lt 3) { return $null }
        # Heuristic: if gzip test fails and size > 20, try brotli
        $ms = [IO.MemoryStream]::new($data)
        $br = [System.IO.Compression.BrotliStream]::new($ms, [IO.Compression.CompressionMode]::Decompress)
        $out = [IO.MemoryStream]::new(); $buf = New-Object byte[] 4096
        while (($n = $br.Read($buf, 0, $buf.Length)) -gt 0) { $out.Write($buf, 0, $n) }
        $br.Dispose(); $ms.Dispose()
        $res = $out.ToArray(); $out.Dispose(); return $res
    } catch { return $null }
}

<#
.SYNOPSIS
    Attempt Gzip decompression of a byte array.
.DESCRIPTION
    Uses System.IO.Compression.GZipStream to decompress if the data appears to be gzip-compressed
    (based on magic bytes 1F 8B). If not gzip or decompression fails, returns $null.
.PARAMETER data
    Byte array to attempt to decompress.
.OUTPUTS
    Decompressed byte array if successful, otherwise $null.
.NOTES
    This function will not throw; it returns $null on failure.
#>
function ConvertFrom-GzipCompression([byte[]]$data) {
    try {
        if ($data.Length -lt 2) { return $null }
        if ($data[0] -ne 0x1F -or $data[1] -ne 0x8B) { return $null }
        $ms = [IO.MemoryStream]::new($data)
        $gz = [IO.Compression.GZipStream]::new($ms, [IO.Compression.CompressionMode]::Decompress)
        $out = [IO.MemoryStream]::new(); $buf = New-Object byte[] 4096
        while (($n = $gz.Read($buf, 0, $buf.Length)) -gt 0) { $out.Write($buf, 0, $n) }
        $gz.Dispose(); $ms.Dispose()
        $res = $out.ToArray(); $out.Dispose(); return $res
    } catch { return $null }
}

<#
.SYNOPSIS
    Decode HTTP chunked transfer encoding from raw byte array.
.DESCRIPTION
    Parses and decodes HTTP/1.1 chunked transfer encoding (hex-size CRLF ... 0 CRLF CRLF).
    If the data does not appear to be chunked, returns the original byte array.
.PARAMETER data
    Byte array to decode.
.OUTPUTS
    Decoded byte array if chunked, otherwise the original byte array.
.NOTES
    This function will not throw; it returns the original data on failure or if not chunked
#>
function ConvertFrom-ChunkedEncoding([byte[]]$data) {
    try {
        $asciiSample = [Text.Encoding]::ASCII.GetString($data, 0, [Math]::Min(64, $data.Length))
        if ($asciiSample -notmatch '^[0-9A-Fa-f]{1,6}\r\n') { return $data } # fast reject
        # Walk the structure; abort if anything inconsistent
        $pos = 0
        $decoded = [IO.MemoryStream]::new()
        while ($pos -lt $data.Length) {
            # read size line
            $lineBytes = New-Object System.Collections.Generic.List[byte]
            while ($pos -lt $data.Length) {
                if ($data[$pos] -eq 13 -and ($pos + 1) -lt $data.Length -and $data[$pos + 1] -eq 10) { $pos += 2; break }
                $lineBytes.Add($data[$pos]); $pos++
                if ($lineBytes.Count -gt 8) { return $data } # unlikely a valid size line if too long
            }
            if ($lineBytes.Count -eq 0) { return $data }
            $sizeHex = [Text.Encoding]::ASCII.GetString($lineBytes.ToArray())
            if ([string]::IsNullOrWhiteSpace($sizeHex)) { return $data }
            $sizeRef = [ref]0
            if (-not [int]::TryParse($sizeHex, [System.Globalization.NumberStyles]::HexNumber, $null, $sizeRef)) { return $data }
            $chunkSize = $sizeRef.Value
            if ($chunkSize -eq 0) {
                # Optionally skip any trailing headers (CRLF sequences) but we stop here
                return $decoded.ToArray()
            }
            if ($pos + $chunkSize -gt $data.Length) { return $data }
            $decoded.Write($data, $pos, $chunkSize)
            $pos += $chunkSize
            # Skip trailing CRLF after each chunk
            if ($pos + 1 -lt $data.Length -and $data[$pos] -eq 13 -and $data[$pos + 1] -eq 10) { $pos += 2 }
        }
        $dec = $decoded.ToArray(); $decoded.Dispose(); return $dec
    } catch { return $data }
}

<#
.SYNOPSIS
    Decode raw HTTP response body bytes that may be chunked and/or compressed.
.DESCRIPTION
    Handles these cases heuristically without relying on higher-level HTTP stacks:
        1. HTTP/1.1 chunked transfer encoding (hex-size CRLF ... 0 CRLF CRLF)
        2. Gzip-compressed payloads (magic 1F 8B)
        3. Brotli-compressed payloads (magic 0xCE B2 CF 81 or common first byte 0x8B w/ fallback) if System.IO.Compression.BrotliStream available
        4. Falls back gracefully to UTF8/Latin1 decoding if decompression fails
    Returns decoded string best-effort; never throws.
.PARAMETER Bytes
    Raw body bytes as captured from socket.
.OUTPUTS
    [string] decoded textual representation (UTF8 preferred; Latin1 fallback)
#>
function Convert-BytesToStringWithGzipScan {
    param(
        [byte[]]$Bytes,
        [switch]$ReturnDiagnostics
    )
    if (-not $Bytes) { return '' }

    $originalLen = $Bytes.Length
    $diagnostics = [ordered]@{ OriginalLength = $originalLen }



    $maybeChunked = ConvertFrom-ChunkedEncoding $Bytes
    if ($maybeChunked -ne $Bytes) { $diagnostics['ChunkedDecoded'] = $true; $diagnostics['AfterChunkLength'] = $maybeChunked.Length } else { $diagnostics['ChunkedDecoded'] = $false }
    $Bytes = $maybeChunked

    $decodedBytes = $null
    $gzipBytes = ConvertFrom-GzipCompression $Bytes
    if ($gzipBytes) { $decodedBytes = $gzipBytes; $diagnostics['GzipDecoded'] = $true }
    else {
        $brotliBytes = ConvertFrom-BrotliCompression $Bytes
        if ($brotliBytes) { $decodedBytes = $brotliBytes; $diagnostics['BrotliDecoded'] = $true }
        else { $decodedBytes = $Bytes; $diagnostics['GzipDecoded'] = $false; $diagnostics['BrotliDecoded'] = $false }
    }

    # Choose encoding: prefer UTF8, fallback Latin1 if invalid
    $text = $null
    try {
        $text = [Text.Encoding]::UTF8.GetString($decodedBytes)
        # Validate by re-encoding (detect replacement char  or � not reliably) but assume fine
    } catch {
        $text = [Text.Encoding]::GetEncoding('ISO-8859-1').GetString($decodedBytes)
        $diagnostics['Latin1Fallback'] = $true
    }

    if ($ReturnDiagnostics) { return [pscustomobject]@{ Text = $text; Meta = $diagnostics } }
    return $text
}

<#
.SYNOPSIS
    Normalize JSON string by parsing and re-serializing with consistent formatting.
.DESCRIPTION
    This function takes a JSON string, parses it into an object, and then re-serializes it
    using ConvertTo-Json with -Compress to produce a normalized representation.
.PARAMETER Json
    The input JSON string to normalize.
.OUTPUTS
    A normalized JSON string.
#>
function Get-NormalizedJson {
    [CmdletBinding()]
    [outputtype([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Json
    )
    $obj = $Json | ConvertFrom-Json -Depth 100
    $newJson = $obj | ConvertTo-Json -Depth 100 -Compress
    return $newJson.Replace('\r\n', '\n')
}


# Load Kestrun module from src/PowerShell/Kestrun if not already loaded
if (-not (Get-Module -Name Kestrun)) {
    $ProjectRoot = Get-ProjectRootDirectory
    if ($ProjectRoot) {
        $modulePath = Join-Path -Path $ProjectRoot -ChildPath 'src' | Join-Path -ChildPath 'PowerShell' | Join-Path -ChildPath 'Kestrun' | Join-Path -ChildPath 'Kestrun.psd1'
        try {
            if (Test-Path $modulePath) { Import-Module $modulePath -Force }
        } catch {
            Write-Warning "Failed to import Kestrun module from $($modulePath) : $_"
        }
    }
}

<#
.SYNOPSIS
    Invoke a CORS request to the specified path with the given Origin header.
.DESCRIPTION
    This function sends an HTTP request to the specified path with the given Origin header.
    It supports both simple and preflight CORS requests.
.PARAMETER Method
    The HTTP method to use for the request (GET, POST, PUT, DELETE, OPTIONS).
.PARAMETER Path
    The relative path to the endpoint.
.PARAMETER Origin
    The Origin header value to include in the request.  This is required for CORS requests.
.PARAMETER PreflightMethod
    The HTTP method to use for the preflight request (if applicable).
#>
function Invoke-CorsRequest {
    param(
        [Parameter(Mandatory)][ValidateSet('GET', 'POST', 'PUT', 'DELETE', 'OPTIONS')]
        [string]$Method,

        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Origin,

        [string]$PreflightMethod,
        [string[]]$PreflightHeaders,

        [string]$Body,
        [string]$ContentType = 'application/json'
    )

    $headers = @{ Origin = $Origin }

    if ($Method -eq 'OPTIONS') {
        if ($PreflightMethod) {
            $headers['Access-Control-Request-Method'] = $PreflightMethod
        }
        if ($PreflightHeaders -and $PreflightHeaders.Count -gt 0) {
            $headers['Access-Control-Request-Headers'] = ($PreflightHeaders -join ', ')
        }
    }

    $uri = "$($script:instance.Url)$Path"

    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        SkipCertificateCheck = $true
        SkipHttpErrorCheck = $true
    }

    if ($Body) {
        $params.Body = $Body
        $params.ContentType = $ContentType
    }

    Invoke-WebRequest @params
}

<#
.SYNOPSIS
    Retrieve a CSRF token from the specified instance URL using the provided web session.
.DESCRIPTION
    This function sends a GET request to the /csrf-token endpoint of the specified instance URL
    using the provided web session. It expects a JSON response containing a CSRF token and
    validates the response status code and token presence.
.PARAMETER InstanceUrl
    The base URL of the instance to retrieve the CSRF token from.
.PARAMETER Session
    The web request session to use for the request.
.OUTPUTS
    The CSRF token as a string.
#>
function Get-CsrfToken {
    param(
        [Parameter(Mandatory)]
        [string]$InstanceUrl,
        [Parameter(Mandatory)]
        [Microsoft.PowerShell.Commands.WebRequestSession]$Session
    )

    $p = @{
        Uri = "$InstanceUrl/csrf-token"
        Method = 'Get'
        UseBasicParsing = $true
        TimeoutSec = 12
        WebSession = $Session
        SkipHttpErrorCheck = $true
        SkipCertificateCheck = $true
    }

    $resp = Invoke-WebRequest @p
    $resp.StatusCode | Should -Be 200

    $payload = $resp.Content | ConvertFrom-Json -ErrorAction Stop
    $payload.token | Should -Not -BeNullOrEmpty
    ($payload.headerName ?? 'X-CSRF-TOKEN') | Should -Be 'X-CSRF-TOKEN'

    return $payload.token
}

<#
.SYNOPSIS
    Extract schema $ref strings from callback request bodies in an OpenAPI document.
.DESCRIPTION
    This function processes a callback object from an OpenAPI document and extracts all schema
    $ref strings found in the request bodies of the operations defined within the callback.
.PARAMETER Doc
    The OpenAPI document as a hashtable or PSCustomObject.
.PARAMETER Callback
    The callback object to process, which may be a reference or an inline definition.
.OUTPUTS
    An array of schema $ref strings found in the callback request bodies.
#>
function Get-CallbackRequestSchemaRefs {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Doc,
        [Parameter(Mandatory)]$Callback
    )

    # Resolve callback references (e.g. {"$ref":"#/components/callbacks/reservationCallback"})
    if ($Callback -is [System.Collections.IDictionary] -and (@($Callback.Keys) -contains '$ref')) {
        $ref = [string]$Callback['$ref']
        if ($ref -match '^#/components/callbacks/(?<name>.+)$') {
            $callbackName = $Matches['name']
            $Callback = $Doc.components.callbacks[$callbackName]
        }
    }

    if ($null -eq $Callback) { return @() }

    $refs = @()

    foreach ($expressionKey in $Callback.Keys) {
        $pathItems = $Callback[$expressionKey]
        foreach ($pathKey in $pathItems.Keys) {
            # Here $expressionKey is the runtime expression (e.g. '{$request.body#/callbackUrls/status}'),
            # and $pathKey is the HTTP verb (typically 'post'); the value is the operation object.
            $operation = $pathItems[$pathKey]
            if ($null -eq $operation) { continue }

            $requestBody = $operation['requestBody']
            if ($null -eq $requestBody) { continue }

            $content = $requestBody['content']
            if ($null -eq $content) { continue }

            foreach ($ct in $content.Keys) {
                $schema = $content[$ct]['schema']
                if ($null -ne $schema -and ($schema -is [System.Collections.IDictionary]) -and (@($schema.Keys) -contains '$ref')) {
                    $refs += [string]$schema['$ref']
                }
            }
        }
    }

    return $refs
}

<#
.SYNOPSIS
    Test that the OpenAPI document from the instance matches the expected document.
.DESCRIPTION
    This function retrieves the OpenAPI document from the specified instance URL and compares it
    to an expected OpenAPI document stored in the Assets/OpenAPI directory. It normalizes both
    documents before comparison to ensure consistent formatting.
.PARAMETER Instance
    The instance object containing the URL and BaseName properties.
.PARAMETER Version
    The OpenAPI version to retrieve (default is 'v3.1').
.NOTES
    The expected OpenAPI document should be located at:
    Assets/OpenAPI/{Instance.BaseName}.json
#>
function Test-OpenApiDocumentMatchesExpected {
    param(
        [Parameter(Mandatory)]
        [Pscustomobject]$Instance,
        [Parameter()]
        [string]$Version = 'v3.1'
    )

    $result = Invoke-WebRequest -Uri "$($Instance.Url)/openapi/$Version/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
    $result.StatusCode | Should -Be 200

    $actualNormalized = Get-NormalizedJson $result.Content
    $expectedPath = Join-Path -Path (Get-TutorialExamplesDirectory) -ChildPath 'Assets' `
        -AdditionalChildPath 'OpenAPI', $Version, "$($Instance.BaseName).json"

    $expectedContent = Get-Content -Path $expectedPath -Raw
    $expectedNormalized = Get-NormalizedJson $expectedContent

    $actualNormalized | Should -Be $expectedNormalized
}

<#
.SYNOPSIS
    Create a Gzip-compressed multipart/form-data body for testing.
.DESCRIPTION
    This function constructs a multipart/form-data body with a text note and a text file,
    then compresses it using Gzip and returns the resulting byte array.
.PARAMETER boundary
    The boundary string to use for the multipart/form-data body.
.OUTPUTS
    A byte array containing the Gzip-compressed multipart/form-data body.
#>
function New-GzipMultipartBody {
    param(
        [string]$boundary
    )
    $body = @(
        "--$boundary",
        'Content-Disposition: form-data; name=note',
        '',
        'compressed',
        "--$boundary",
        'Content-Disposition: form-data; name=file; filename=hello.txt',
        'Content-Type: text/plain',
        '',
        'hello',
        "--$boundary--",
        ''
    ) -join "`r`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $ms = [System.IO.MemoryStream]::new()
    try {
        $gzip = [System.IO.Compression.GZipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress, $true)
        try {
            $gzip.Write($bytes, 0, $bytes.Length)
        } finally {
            $gzip.Dispose()
        }

        # IMPORTANT: prevent PowerShell from enumerating the byte[] on output.
        # Invoke-WebRequest expects a single byte[] object for raw bodies.
        return , $ms.ToArray()
    } finally {
        $ms.Dispose()
    }
}

<#
.SYNOPSIS
    Create a Gzip-compressed byte array from input data.
.DESCRIPTION
    This function takes a byte array as input, compresses it using Gzip, and returns
    the resulting compressed byte array.
.PARAMETER data
    The input byte array to compress.
.OUTPUTS
    A byte array containing the Gzip-compressed data.
#>
function New-GzipBinaryData {
    param(
        [byte[]]$data
    )
    $ms = [System.IO.MemoryStream]::new()
    try {
        $gzip = [System.IO.Compression.GZipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress, $true)
        try {
            $gzip.Write($data, 0, $data.Length)
        } finally {
            $gzip.Dispose()
        }

        # Prevent byte[] enumeration on output.
        return , $ms.ToArray()
    } finally {
        $ms.Dispose()
    }
}

<#
.SYNOPSIS
    Create a multipart/form-data body from parts.
.DESCRIPTION
    This function constructs a multipart/form-data body using the specified boundary and parts.
    Each part is defined by a hashtable containing Headers and Content.
.PARAMETER Boundary
    The boundary string to use for the multipart/form-data body.
.PARAMETER Parts
    An array of hashtables, each representing a part with Headers and Content.
.OUTPUTS
    A string containing the multipart/form-data body.
#>
function New-MultipartBody {
    param(
        [string]$Boundary,
        [hashtable[]]$Parts
    )

    $body = ''
    foreach ($part in $Parts) {
        $body += "--$Boundary`r`n"

        if ($part.Headers) {
            foreach ($header in $part.Headers.GetEnumerator()) {
                $body += "$($header.Key): $($header.Value)`r`n"
            }
        }

        $body += "`r`n$($part.Content)`r`n"
    }
    $body += "--$Boundary--`r`n"

    return $body
}

<#
.SYNOPSIS
    Compress a string using Gzip compression.
.DESCRIPTION
    This function takes a string as input, compresses it using Gzip, and returns
    the resulting compressed byte array.
.PARAMETER Data
    The input string to compress.
.OUTPUTS
    A byte array containing the Gzip-compressed data.
#>
function Compress-Gzip {
    param([string]$Data)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Data)
    $ms = [System.IO.MemoryStream]::new()
    $gzip = [System.IO.Compression.GzipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress)
    $gzip.Write($bytes, 0, $bytes.Length)
    $gzip.Close()
    $compressed = $ms.ToArray()
    $ms.Dispose()

    return $compressed
}

<#
.SYNOPSIS
    Create a nested multipart/mixed body for testing.
.DESCRIPTION
    This function constructs a multipart/mixed body with an outer and inner boundary,
    allowing for nested multipart content. It supports optional Content-Disposition headers
    for the outer and nested parts, as well as for the inner text and JSON parts.
.PARAMETER OuterBoundary
    The boundary string for the outer multipart section.
.PARAMETER InnerBoundary
    The boundary string for the inner multipart section.
.PARAMETER IncludeOuterDisposition
    If specified, includes a Content-Disposition header for the outer part.
.PARAMETER IncludeNestedDisposition
    If specified, includes a Content-Disposition header for the nested part.
.PARAMETER IncludeInnerDispositions
    If specified, includes Content-Disposition headers for the inner text and JSON parts.
.PARAMETER OuterName
    The name to use in the Content-Disposition header for the outer part.
.PARAMETER NestedName
    The name to use in the Content-Disposition header for the nested part.
.PARAMETER TextName
    The name to use in the Content-Disposition header for the inner text part.
.PARAMETER JsonName
    The name to use in the Content-Disposition header for the inner JSON part.
.PARAMETER OuterJson
    The JSON content for the outer part.
.PARAMETER InnerJson
    The JSON content for the inner part.
.PARAMETER InnerText
    The text content for the inner text part.
.PARAMETER NestedContentTypeHeader
    The Content-Type header to use for the nested part. If not specified, defaults to multipart/mixed with the inner boundary.
.OUTPUTS
    A string containing the nested multipart/mixed body.
#>
function New-NestedMultipartBody {
    param(
        [string] $OuterBoundary = 'outer-boundary',
        [string] $InnerBoundary = 'inner-boundary',
        [switch] $IncludeOuterDisposition,
        [switch] $IncludeNestedDisposition,
        [switch] $IncludeInnerDispositions,
        [string] $OuterName = 'outer',
        [string] $NestedName = 'nested',
        [string] $TextName = 'text',
        [string] $JsonName = 'json',
        [string] $OuterJson = '{"stage":"outer"}',
        [string] $InnerJson = '{"nested":true}',
        [string] $InnerText = 'inner-1',
        [string] $NestedContentTypeHeader = $null
    )

    if (-not $NestedContentTypeHeader) {
        $NestedContentTypeHeader = "Content-Type: multipart/mixed; boundary=$InnerBoundary"
    }

    $innerBody = @(
        "--$InnerBoundary"
        if ($IncludeInnerDispositions) { "Content-Disposition: form-data; name=""$TextName""" }
        'Content-Type: text/plain'
        ''
        $InnerText

        "--$InnerBoundary"
        if ($IncludeInnerDispositions) { "Content-Disposition: form-data; name=""$JsonName""" }
        'Content-Type: application/json'
        ''
        $InnerJson

        "--$InnerBoundary--"
        ''
    ) -join "`r`n"

    $outerBody = @(
        "--$OuterBoundary"
        if ($IncludeOuterDisposition) { "Content-Disposition: form-data; name=""$OuterName""" }
        'Content-Type: application/json'
        ''
        $OuterJson

        "--$OuterBoundary"
        if ($IncludeNestedDisposition) { "Content-Disposition: form-data; name=""$NestedName""" }
        $NestedContentTypeHeader
        ''
        $innerBody

        "--$OuterBoundary--"
        ''
    ) -join "`r`n"

    return $outerBody
}

<#
.SYNOPSIS
    Write detailed diagnostics of a Kestrun example instance on test failure.
.DESCRIPTION
    This function checks the Pester test result and, if the test has failed,
    it outputs detailed information about the provided Kestrun example instance.
    This includes core metadata, process information, standard error/output,
    and a full JSON dump of the instance for debugging purposes.
.PARAMETER Instance
    The Kestrun example instance object to output diagnostics for.
.PARAMETER Label
    An optional label to identify the instance in the output. Default is 'Example instance'.
#>
function Write-KrExampleInstanceOnFailure {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [psobject] $Instance,

        [Parameter()]
        [string] $Label
    )

    $failedTests = Get-KrPesterFailedTestsInCurrentBlock
    if ($failedTests.Count -eq 0) { return }

    if (-not $Label) {
        $Label = $Instance.Name
    }

    $safeBaseName = if ($Instance.BaseName) {
        $Instance.BaseName -replace '[^\w\.-]+', '_'
    } else {
        'unknown-example'
    }

    $diagDir = Join-Path -Path 'TestResults' -ChildPath 'Diagnostics' -AdditionalChildPath $safeBaseName
    New-Item -ItemType Directory -Force -Path $diagDir | Out-Null

    Write-Host ''
    Write-Host "=== $Label (failure diagnostics) ===" -ForegroundColor Yellow
    Write-Host "Saving diagnostics to: $diagDir" -ForegroundColor Cyan

    # Core metadata to screen
    $Instance |
        Select-Object `
            Name,
        BaseName,
        Url,
        Host,
        Port,
        TempPath,
        Ready,
        ExitedEarly,
        Https |
        Format-List * |
        Out-String |
        Write-Host

    # Copy raw stdout/stderr log files if they exist
    if ($Instance.StdOut -and (Test-Path -LiteralPath $Instance.StdOut)) {
        Copy-Item -LiteralPath $Instance.StdOut `
            -Destination (Join-Path $diagDir 'stdout.log') `
            -Force
        Write-Host "Copied StdOut log to $(Join-Path $diagDir 'stdout.log')" -ForegroundColor DarkGray
    } elseif ($Instance.StdOut) {
        Write-Host "StdOut path not found: $($Instance.StdOut)" -ForegroundColor Red
    }

    if ($Instance.StdErr -and (Test-Path -LiteralPath $Instance.StdErr)) {
        Copy-Item -LiteralPath $Instance.StdErr `
            -Destination (Join-Path $diagDir 'stderr.log') `
            -Force
        Write-Host "Copied StdErr log to $(Join-Path $diagDir 'stderr.log')" -ForegroundColor DarkGray
    } elseif ($Instance.StdErr) {
        Write-Host "StdErr path not found: $($Instance.StdErr)" -ForegroundColor Red
    }

    # Process info
    $processInfo = $null
    if ($Instance.Process) {
        try {
            $p = $Instance.Process
            $processInfo = [pscustomobject]@{
                Id = $p.Id
                HasExited = $p.HasExited
                ExitCode = if ($p.HasExited) { $p.ExitCode } else { $null }
            }

            Write-Host '=== Process ===' -ForegroundColor Yellow
            Write-Host "Id=$($processInfo.Id) HasExited=$($processInfo.HasExited) ExitCode=$($processInfo.ExitCode)"
        } catch {
            Write-Host "Failed to retrieve process info: $_" -ForegroundColor Red
        }
    }

    # JSON snapshot
    $diag = [pscustomobject]@{
        Label = $Label
        Name = $Instance.Name
        BaseName = $Instance.BaseName
        Url = $Instance.Url
        Host = $Instance.Host
        Port = $Instance.Port
        TempPath = $Instance.TempPath
        Ready = $Instance.Ready
        ExitedEarly = $Instance.ExitedEarly
        Https = $Instance.Https
        StdOutPath = $Instance.StdOut
        StdErrPath = $Instance.StdErr
        Process = $processInfo
    } | ConvertTo-Json -Depth 4

    $jsonPath = Join-Path $diagDir 'instance.json'
    $diag | Set-Content -LiteralPath $jsonPath -Encoding utf8

    Write-Host "Saved JSON snapshot to $jsonPath" -ForegroundColor Cyan

    # STDERR / STDOUT (super useful after stop)
    if ($Instance.StdErr) {
        Write-Host '=== StdErr ===' -ForegroundColor Red
        $Instance.StdErr | Write-Host
    }

    if ($Instance.StdOut) {
        Write-Host '=== StdOut ===' -ForegroundColor DarkGray
        $Instance.StdOut | Write-Host
    }

    # Full JSON dump (for CI / copy-paste)
    Write-Host '=== Full instance (JSON) ===' -ForegroundColor Yellow
    Write-Host $diag
}

<#
.SYNOPSIS
    Get the failed Pester tests in the current test block.
.DESCRIPTION
    This function retrieves the list of failed tests from the current Pester test block.
    It checks the test results and filters for tests that have failed or have an associated error record.
.OUTPUTS
    An array of objects representing the failed tests.
#>
function Get-KrPesterFailedTestsInCurrentBlock {
    [CmdletBinding()]
    [outputtype([object[]])]
    param()

    try {
        $items = @(
            $____Pester.CurrentBlock.Tests |
                Where-Object { $_.Result -eq 'Failed' -or $_.ErrorRecord }
        )

        return , $items   # <-- ALWAYS returns an array (0/1/many)
    } catch {
        return , @()      # <-- ALSO always array
    }
}

<#
.SYNOPSIS
    Create a test file with specified size and mode (text or binary).
.DESCRIPTION
    This function generates a file at the specified path with the desired size in megabytes.
    The file can be created in either text mode (compressible data) or binary mode (random data).
.PARAMETER Path
    The file path where the test file will be created.
.PARAMETER Mode
    The mode of the file to create: 'Text' for compressible text data, 'Binary' for random binary data. Default is 'Text'.
.PARAMETER SizeMB
    The size of the file to create in megabytes. Default is 100 MB.
.NOTES
    This function will overwrite any existing file at the specified path.
#>
function New-TestFile {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    param(
        [parameter(Mandatory = $true)]
        [string]$Path,
        [parameter()]
        [ValidateSet('Text', 'Binary')]
        [string]$Mode = 'Text',
        [parameter()]
        [long]$SizeMB = 100,
        [Parameter()]
        [switch]$Force,
        [Parameter()]
        [switch]$Quiet
    )

    # ─── Setup ────────────────────────────────────────────────────────
    $targetBytes = $SizeMB * 1MB

    if (-not $Quiet) {
        Write-Host "Generating $Mode test file of size $SizeMB MB at $Path ..."
        Write-Host -NoNewline 'Progress: '
    }

    if (Test-Path $Path) {
        if (-not $Force) {
            throw "File already exists at $Path. Use -Force to overwrite."
        }
        Remove-Item $Path
    }

    # ─── Generate Binary File ─────────────────────────────────────────
    if ($Mode -eq 'Binary') {
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        $buffer = [byte[]]::new(1MB)
        $written = 0
        try {
            $fs = [System.IO.File]::OpenWrite($Path)

            while ($written -lt $targetBytes) {
                $rng.GetBytes($buffer)
                $remaining = $targetBytes - $written
                $bytesToWrite = if ($buffer.Length -lt $remaining) { $buffer.Length } else { [long]$remaining }
                $fs.Write($buffer, 0, $bytesToWrite)
                $written += $bytesToWrite
                if (($written % (10MB) -eq 0) -and (-not $Quiet)) {
                    # Print progress every 10MB
                    Write-Host '#' -NoNewline
                }
            }
        } finally {
            $fs.Close()
        }
        if (-not $Quiet) {
            Write-Host ' ✅' -ForegroundColor Green
        }
    }

    # ─── Generate Compressible Text File ──────────────────────────────
    if ($Mode -eq 'Text') {
        $baseLine = 'COMPRESSIBLE-DATA-LINE-1234567890'
        $entropyRate = 1000  # Inject entropy every N lines
        $lineTemplate = $baseLine * 5 + "`n"  # ~180–200 bytes
        $lineBytes = [System.Text.Encoding]::UTF8.GetByteCount($lineTemplate)
        $lineCount = [math]::Ceiling($targetBytes / $lineBytes)

        $rand = [System.Random]::new()
        try {
            $writer = [System.IO.StreamWriter]::new($Path, $false, [System.Text.Encoding]::UTF8)

            for ($i = 0; $i -lt $lineCount; $i++) {
                if ($i % $entropyRate -eq 0) {
                    # Inject a small random string
                    $randomSuffix = $rand.Next(100000, 999999)
                    $line = "$baseLine-$randomSuffix" * 5 + "`n"
                } else {
                    $line = $lineTemplate
                }
                # Write the line to the file
                $writer.Write($line)
                if (($i % (63700) -eq 0 ) -and (-not $Quiet)) {
                    Write-Host '#' -NoNewline
                }
            }
        } finally {
            $writer.Close()
        }
        if (-not $Quiet) {
            Write-Host ' ✅' -ForegroundColor Green
        }
    }
}

<#
.SYNOPSIS
    Retrieve the SIDs associated with a specific Windows privilege right.
.DESCRIPTION
    This function exports the local security policy for user rights, parses the relevant section,
    and extracts the SIDs associated with the specified privilege right (e.g., SeServiceLogonRight).
    It returns an array of SIDs in string format. The function handles cleanup of temporary files used for exporting the policy.
.PARAMETER RightName
    The name of the Windows privilege right to retrieve SIDs for (e.g., 'SeServiceLogonRight').
.PARAMETER WorkingDirectory
    The directory to use for temporary files during the export process. This should be a valid writable directory path.
.OUTPUTS
    An array of strings representing the SIDs associated with the specified privilege right. If no SIDs are found or if an error occurs, an empty array is returned.
#>
function Get-WindowsPrivilegeRightSid {
    param(
        [Parameter(Mandatory)]
        [string]$RightName,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory
    )

    $exportPath = Join-Path $WorkingDirectory ('secpol-export-{0}.inf' -f ([Guid]::NewGuid().ToString('N')))
    try {
        & secedit.exe /export /cfg $exportPath /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exportPath)) {
            throw 'Failed to export local security policy for USER_RIGHTS.'
        }

        $line = Select-String -Path $exportPath -Pattern "^$([regex]::Escape($RightName))\s*=\s*(.*)$" -CaseSensitive | Select-Object -First 1
        if (-not $line) {
            return @()
        }

        $rawValue = $line.Matches[0].Groups[1].Value.Trim()
        if ([string]::IsNullOrWhiteSpace($rawValue)) {
            return @()
        }

        return @($rawValue.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
    } finally {
        Remove-Item -LiteralPath $exportPath -Force -ErrorAction SilentlyContinue
    }
}

<#
.SYNOPSIS
    Retrieve the SIDs associated with the SeServiceLogonRight privilege right.
.DESCRIPTION
    This function is a specific wrapper around Get-WindowsPrivilegeRightSid to retrieve the SIDs associated with the SeServiceLogonRight privilege right, which allows service logon.
    It requires a working directory for temporary files used during the export process. The function returns an array of SIDs in string format for the SeServiceLogonRight privilege right.
    If no SIDs are found or if an error occurs, an empty array is returned.
.PARAMETER WorkingDirectory
    The directory to use for temporary files during the export process. This should be a valid writable directory path.
.OUTPUTS
    An array of strings representing the SIDs associated with the SeServiceLogonRight privilege right. If no SIDs are found or if an error occurs, an empty array is returned.
#>
function Get-WindowsServiceLogonRightSid {
    param(
        [Parameter(Mandatory)]
        [string]$WorkingDirectory
    )

    return @(Get-WindowsPrivilegeRightSid -RightName 'SeServiceLogonRight' -WorkingDirectory $WorkingDirectory)
}

<#
.SYNOPSIS
    Retrieve the SIDs associated with the SeDenyServiceLogonRight privilege right.
.DESCRIPTION
    This function is a specific wrapper around Get-WindowsPrivilegeRightSid to retrieve the SIDs associated with the SeDenyServiceLogonRight privilege right, which denies service logon.
    It requires a working directory for temporary files used during the export process. The function returns an array of SIDs in string format for the SeDenyServiceLogonRight privilege right.
    If no SIDs are found or if an error occurs, an empty array is returned.
.PARAMETER WorkingDirectory
    The directory to use for temporary files during the export process. This should be a valid writable directory path.
.OUTPUTS
    An array of strings representing the SIDs associated with the SeDenyServiceLogonRight privilege right. If no SIDs are found or if an error occurs, an empty array is returned
#>
function Get-WindowsDenyServiceLogonRightSid {
    param(
        [Parameter(Mandatory)]
        [string]$WorkingDirectory
    )

    return @(Get-WindowsPrivilegeRightSid -RightName 'SeDenyServiceLogonRight' -WorkingDirectory $WorkingDirectory)
}

<#
.SYNOPSIS
    Set the SIDs for the SeServiceLogonRight privilege right.
.DESCRIPTION
    This function takes an array of SIDs and sets them as the assigned SIDs for the SeServiceLogonRight privilege right in the local security policy.
    It creates a temporary INF file with the appropriate format and uses secedit.exe to apply the configuration. The function ensures cleanup of temporary files after execution.
    If the configuration fails, it throws an error.
.PARAMETER Sids
    An array of strings representing the SIDs to assign to the SeServiceLogonRight privilege right. Each SID should be in the format "*S-1-5-21-..." (with an asterisk prefix).
.PARAMETER WorkingDirectory
    The directory to use for temporary files during the configuration process. This should be a valid writable directory path.
.OUTPUTS
    None. The function performs an action to set the SIDs for the SeServiceLogonRight privilege right and does not return a value. If the operation is successful, it completes silently; if it fails, it throws an error.
#>
function Set-WindowsServiceLogonRightSid {
    param(
        [Parameter(Mandatory)]
        [string[]]$Sids,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory
    )

    $cfgPath = Join-Path $WorkingDirectory ('secpol-set-{0}.inf' -f ([Guid]::NewGuid().ToString('N')))
    $dbPath = Join-Path $WorkingDirectory ('secpol-set-{0}.sdb' -f ([Guid]::NewGuid().ToString('N')))

    try {
        $sidCsv = ($Sids | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique) -join ','
        $content = @(
            '[Unicode]'
            'Unicode=yes'
            '[Version]'
            'signature="$CHICAGO$"'
            'Revision=1'
            '[Privilege Rights]'
            "SeServiceLogonRight = $sidCsv"
        ) -join [Environment]::NewLine

        Set-Content -LiteralPath $cfgPath -Value $content -Encoding ascii
        & secedit.exe /configure /db $dbPath /cfg $cfgPath /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to configure local security policy USER_RIGHTS.'
        }
    } finally {
        Remove-Item -LiteralPath $cfgPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $dbPath -Force -ErrorAction SilentlyContinue
    }
}

<#
.SYNOPSIS
    Convert an account name to a policy SID.
.DESCRIPTION
    This function takes an account name and converts it to a policy SID in the format "*S-1-5-21-...".
.PARAMETER AccountName
    The name of the account to convert. This should be in the format "DOMAIN\User" or "User".
.OUTPUTS
    A string representing the policy SID for the specified account.
#>
function Convert-AccountToPolicySid {
    param(
        [Parameter(Mandatory)]
        [string]$AccountName
    )

    $sid = ([System.Security.Principal.NTAccount]::new($AccountName)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    return "*$sid"
}
