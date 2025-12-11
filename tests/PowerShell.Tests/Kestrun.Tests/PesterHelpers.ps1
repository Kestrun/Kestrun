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
    $root = (Get-Item (Split-Path -Parent $PSCommandPath))
    while ($root -and -not (Test-Path (Join-Path $root.FullName 'Kestrun.sln'))) {
        $parent = Get-Item (Split-Path -Parent $root.FullName) -ErrorAction SilentlyContinue
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
    $root = (Get-Item (Split-Path -Parent $PSCommandPath))
    while ($root -and -not (Test-Path (Join-Path $root.FullName 'Kestrun.sln'))) {
        $parent = Get-Item (Split-Path -Parent $root.FullName) -ErrorAction SilentlyContinue
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
    $root = (Get-Item (Split-Path -Parent $PSCommandPath))
    while ($root -and -not (Test-Path (Join-Path $root.FullName 'Kestrun.sln'))) {
        $parent = Get-Item (Split-Path -Parent $root.FullName) -ErrorAction SilentlyContinue
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
    param(
        [int]$FallbackPort = 5000,
        [int]$MaxPort = 64000
    )
    try {
        $retryCount = 0
        $listener = $null
        do {
            if ($null -ne $listener) {
                $listener.Stop()
                $listener.Dispose()
            }
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            $port = ($listener.LocalEndpoint).Port
            $retryCount++
        } until ($port -lt $MaxPort -or $retryCount -ge 5)
    } catch {
        $port = $FallbackPort # fallback
        Write-Warning "Failed to get free TCP port: $_ (using fallback port $FallbackPort)"
    } finally {
        if ($null -ne $listener) {
            $listener.Stop()
            $listener.Dispose()
        }
    }
    return $port
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
    Maximum time to wait for the example to start accepting connections. Default is 15 seconds.
.PARAMETER HttpProbeDelayMs
    Delay between HTTP probes of the root URL when waiting for startup. Default is 150ms.
.PARAMETER FromRootDirectory
    If specified, resolves example script paths relative to the repository root instead of the module directory.
.OUTPUTS
    A custom object with properties:
    - Name: The name of the example script.
    - Port: The TCP port number the example is listening on.
    - TempPath: The path to the temporary modified script file.
    - Process: The Process object of the started example.
    - Content: The modified script content that was run.
    - StdOut: Path to the redirected standard output log file.
    - StdErr: Path to the redirected standard error log file.
    - ExitedEarly: Boolean indicating if the process exited before startup completed.
    - Ready: Boolean indicating if the example is ready to accept connections.
#>
function Start-ExampleScript {
    [CmdletBinding(SupportsShouldProcess, defaultParameterSetName = 'Name')]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Name')]
        [string]$Name,
        [Parameter(Mandatory = $true, ParameterSetName = 'ScriptBlock')]
        [scriptblock]$ScriptBlock,
        [int]$Port,
        [int]$StartupTimeoutSeconds = 40,
        [int]$HttpProbeDelayMs = 150,
        [switch]$FromRootDirectory,
        [string[]]$EnvironmentVariables = @('UPSTASH_REDIS_URL')
    )
    if (-not $Port) { $Port = Get-FreeTcpPort }
    if ($PSCmdlet.ParameterSetName -eq 'Name') {
        if ( $FromRootDirectory ) {
            $root = Resolve-Path "$PSScriptRoot\..\..\.."
            $path = Join-Path $root $Name
            if (-not (Test-Path $path)) { throw "Example script not found: $Name" }
        } else {
            $path = Get-ExampleScriptPath -Name $Name
        }
        $scriptDir = Split-Path -Parent $path
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
    $tempDir = [System.IO.Path]::GetTempPath()
    # Generate a unique file name for the temp script
    $fileNameWithoutExtension = ([string]::IsNullOrEmpty($Name)) ? 'ScriptBlockExample' : [IO.Path]::GetFileNameWithoutExtension((Split-Path -Leaf $Name))

    # Write modified legacy content to temp file
    $scriptToRun = Join-Path $tempDir ('kestrun-example-' + $fileNameWithoutExtension + '-' + [System.IO.Path]::GetRandomFileName() + '.ps1')
    Set-Content -Path $scriptToRun -Value $content -Encoding UTF8

    $stdOut = Join-Path $tempDir ('kestrun-example-' + $fileNameWithoutExtension + '-' + [System.IO.Path]::GetRandomFileName() + '.out.log')
    $stdErr = Join-Path $tempDir ('kestrun-example-' + $fileNameWithoutExtension + '-' + [System.IO.Path]::GetRandomFileName() + '.err.log')
    #   $argList = @('-NoLogo', '-NoProfile', '-File', $scriptToRun, '-Port', $Port)

    # Build environment variables for the child process (including UPSTASH_REDIS_URL if set in parent)
    $environment = @{}
    # Copy current environment variables
    foreach ($key in [System.Environment]::GetEnvironmentVariables().Keys) {
        if ($EnvironmentVariables -contains $key) {
            $environment[$key] = [System.Environment]::GetEnvironmentVariable($key)
        }
    }

    # This becomes: pwsh -NoLogo -NoProfile -Command "Import-Module Kestrun; . 'C:\...\myscript.ps1'"
    $argList = @(
        '-NoLogo'
        '-NoProfile'
        '-Command'
        "Import-Module '$kestrunModulePath'; . '$scriptToRun' -Port $Port"
    )

    # Create process start parameters
    $param = @{
        FilePath = 'pwsh'
        WorkingDirectory = $scriptDir
        ArgumentList = $argList
        PassThru = $true
        RedirectStandardOutput = $stdOut
        RedirectStandardError = $stdErr
        Environment = $environment
    }



    # Prevent spawned process from inheriting the test runner's console window on Windows (avoids unwanted UI popups during automated tests)
    if ($IsWindows) { $param.WindowStyle = 'Hidden' }
    # Start the process
    $proc = Start-Process @param

    # Wait for the process to start accepting connections or timeout
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $ready = $false
    $attempt = 0
    Start-Sleep -Seconds 1 # Initial delay before probing
    while ([DateTime]::UtcNow -lt $deadline -and -not $ready) {
        if ($proc.HasExited) { break }
        $attempt++
        # Optional lightweight HTTP/HTTPS probe of '/' and '/online' endpoints to detect readiness
        $probeUrls = @(
            "http://$serverIp`:$Port/online",
            "https://$serverIp`:$Port/online",
            "http://$serverIp`:$Port/",
            "https://$serverIp`:$Port/"
        )
        foreach ($url in $probeUrls) {
            try {
                $probe = Get-HttpHeadersRaw -Uri $url -Insecure -AsHashtable
                if ($probe.StatusCode -ge 200 -and $probe.StatusCode -lt 600) {
                    $ready = $true
                    break
                }
            } catch {
                $script:errorMessage = $_.Exception.Message
            }
        }
        Start-Sleep -Milliseconds $HttpProbeDelayMs
    }
    $exited = $proc.HasExited
    if (-not $ready -and -not $exited) {
        if ($errorMessage) {
            Write-Warning "Example $Name not accepting connections on port $Port after timeout. Last probe error: $errorMessage. Continuing; requests may fail."
        } else {
            Write-Warning "Example $Name not accepting connections on port $Port after timeout. Continuing; requests may fail."
        }
    }

    if ($exited) {
        Write-Warning "Example $Name process exited early with code $($proc.ExitCode). Capturing logs."
        if (Test-Path $stdErr) { Write-Warning ('stderr: ' + (Get-Content $stdErr -Raw)) }
        if (Test-Path $stdOut) { Write-Verbose ('stdout: ' + (Get-Content $stdOut -Raw)) -Verbose }
    }

    # Heuristic: detect HTTPS usage if listener line includes cert/self-signed flags
    $usesHttps = $false
    if (($content -match 'Add-KrEndpoint[^\n]*-SelfSignedCert') -or
        ($content -match 'Add-KrEndpoint[^\n]*-CertPath') -or
        ($content -match 'Add-KrEndpoint[^\n]*-X509Certificate')
    ) {
        $usesHttps = $true
    }

    Start-Sleep -Seconds 2 # Allow some time for server to stabilize

    return [pscustomobject]@{
        Name = $Name
        Url = ('{0}://{1}:{2}' -f ($usesHttps ? 'https' : 'http'), $serverIp, $Port)
        Host = $serverIp
        Port = $Port
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
    [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)]$Instance)
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
        if (-not $Instance.Process.HasExited) { $Instance.Process | Stop-Process -Force }
        $Instance.Process.Dispose()
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
        try { $http.Dispose() } catch {}
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
        try { if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) { $socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'done', $cts.Token).Wait() } } catch {}
        try { $socket.Dispose() } catch {}
        try { $cts.Dispose() } catch {}
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
        [switch]$NoAcceptEncoding
    )

    $u = [Uri]$Uri
    if ($u.Scheme -notin @('http', 'https')) {
        throw "Unsupported scheme '$($u.Scheme)'. Use http or https."
    }

    $targetHost = if ($HostOverride) { $HostOverride } else { $u.Host }
    $port = if ($u.IsDefaultPort) { if ($u.Scheme -eq 'https') { 443 } else { 80 } } else { $u.Port }
    $path = if ([string]::IsNullOrEmpty($u.PathAndQuery)) { '/' } else { $u.PathAndQuery }
    $crlf = "`r`n"

    $client = [System.Net.Sockets.TcpClient]::new()
    $client.Connect($u.Host, $port)   # connect to endpoint (IP or host)
    $netStream = $client.GetStream()
    $stream = $null

    try {
        if ($u.Scheme -eq 'https') {
            $cb = if ($Insecure) {
                [System.Net.Security.RemoteCertificateValidationCallback] { param($s, $c, $ch, $e) $true }
            } else { $null }

            $ssl = [System.Net.Security.SslStream]::new($netStream, $false, $cb)
            $proto = [System.Security.Authentication.SslProtocols]::Tls12 -bor `
                [System.Security.Authentication.SslProtocols]::Tls13
            # SNI uses targetHost (can be different from the IP/endpoint you connect to)
            $ssl.AuthenticateAsClient($targetHost, $null, $proto, $false)
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
        $stream.Write($reqBytes, 0, $reqBytes.Length)

        # Read entire response into memory (connection: close => server will close)
        $buf = [byte[]]::new(8192)
        $ms = [System.IO.MemoryStream]::new()
        while (($n = $stream.Read($buf, 0, $buf.Length)) -gt 0) {
            $ms.Write($buf, 0, $n)
        }
        $all = $ms.ToArray()

        # Locate CRLFCRLF separator between headers and body
        $sep = -1
        for ($i = 0; $i -le $all.Length - 4; $i++) {
            if (($all[$i] -eq 13) -and ($all[$i + 1] -eq 10) -and ($all[$i + 2] -eq 13) -and ($all[$i + 3] -eq 10)) {
                $sep = $i; break
            }
        }

        if ($sep -lt 0) {
            # No header terminator found; return raw text or a simple bag
            $rawText = $utf8.GetString($all)
            if ($AsHashtable -or $IncludeBody) {
                return [ordered]@{ 'Status-Line' = '(unknown)'; 'Raw' = $rawText }
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
    return $newJson.Replace("\r\n", "\n")
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
