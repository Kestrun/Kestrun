# Shared helper functions for tutorial example tests
# Provides utilities to locate example scripts, start them on random port, collect routes, and stop.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Write-Debug '[TutorialHelper] Loading helper script' -Verbose

if (-not (Get-Module -Name Kestrun)) {
    $rootSeek = Split-Path -Parent $PSCommandPath
    while ($rootSeek -and -not (Test-Path (Join-Path $rootSeek 'Kestrun.sln'))) {
        $parent = Split-Path -Parent $rootSeek
        if ($parent -eq $rootSeek) { break }
        $rootSeek = $parent
    }
    if ($rootSeek) {
        $modulePath = Join-Path $rootSeek 'src' | Join-Path -ChildPath 'PowerShell' | Join-Path -ChildPath 'Kestrun' | Join-Path -ChildPath 'Kestrun.psm1'
        if (Test-Path $modulePath) { Import-Module $modulePath -Force }
    }
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
    [CmdletBinding()] param()
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ($listener.LocalEndpoint).Port
    $listener.Stop()
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
.PARAMETER Port
    Optional explicit port number to use. If not provided, a free port will be selected.
.PARAMETER StartupTimeoutSeconds
    Maximum time to wait for the example to start accepting connections. Default is 15 seconds.
.PARAMETER HttpProbeDelayMs
    Delay between HTTP probes of the root URL when waiting for startup. Default is 150ms.
.PARAMETER StartupSentinels
    Array of strings to look for in stdout as indicators of successful startup. Default includes common phrases.
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
    [CmdletBinding(SupportsShouldProcess)] param(
        [Parameter(Mandatory)][string]$Name,
        [int]$Port,
        [int]$StartupTimeoutSeconds = 40,
        [int]$HttpProbeDelayMs = 150
    )
    if (-not $Port) { $Port = Get-FreeTcpPort }
    $path = Get-ExampleScriptPath -Name $Name
    $scriptDir = Split-Path -Parent $path
    $originalLocation = Get-Location
    # Push current location so relative file references (Assets/...) resolve inside example
    Push-Location -Path $scriptDir
    $original = Get-Content -Path $path -Raw
    $serverIp = 'localhost' # Use loopback for safety

    $content = $original
    $kestrunModulePath = Get-KestrunModulePath
    $importKestrunModule = @"
if (-not (Get-Module -Name Kestrun)) {
     if (Test-Path -Path '$kestrunModulePath' -PathType Leaf) {
        Import-Module '$kestrunModulePath' -Force -ErrorAction Stop
    } else {
        throw "Kestrun module not found at $kestrunModulePath"
    }
}
"@

    $pattern = '(?ms)^\s*param\s*\(.*?\)'

    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        $pattern,
        { param($m) $m.Value + "`n`n" + $importKestrunModule },
        1  # replace only first occurrence
    )

    if (-not $content.Contains('-Pattern "/shutdown"')) {
        # Inject shutdown endpoint for legacy scripts (first occurrence of Start-KrServer)

        $content = [System.Text.RegularExpressions.Regex]::Replace(
            $content,
            '\bStart-KrServer\b', @"
Add-KrMapRoute -Verbs Get -Pattern "/shutdown" -ScriptBlock { Stop-KrServer }
Add-KrMapRoute -Verbs Get -Pattern "/health" -ScriptBlock { write-KrTextResponse -InputObject 'OK' -StatusCode 200 }
Start-KrServer
"@
            , 1 # only first occurrence
        )
    }


    # Adjust Initialize-KrRoot if present to the example script directory
    if ( $content.Contains('Initialize-KrRoot -Path $PSScriptRoot')) {
        $content = $content.Replace('Initialize-KrRoot -Path $PSScriptRoot', "Initialize-KrRoot -Path '$scriptDir'")
    }
    $tempDir = [System.IO.Path]::GetTempPath()

    # Write modified legacy content to temp file
    $tmp = Join-Path $tempDir ("kestrun-example-" + [System.IO.Path]::GetRandomFileName() + '.ps1')
    Set-Content -Path $tmp -Value $content -Encoding UTF8


    $stdOut = Join-Path $tempDir ("kestrun-example-" + [System.IO.Path]::GetRandomFileName() + '.out.log')
    $stdErr = Join-Path $tempDir ("kestrun-example-" + [System.IO.Path]::GetRandomFileName() + '.err.log')
    $argList = @('-NoLogo', '-NoProfile', '-File', $tmp, '-Port', $Port)

    $param = @{
        FilePath = 'pwsh'
        WorkingDirectory = $scriptDir
        ArgumentList = $argList
        PassThru = $true
        RedirectStandardOutput = $stdOut
        RedirectStandardError = $stdErr
    }
    if ($IsWindows) { $param.WindowStyle = 'Hidden' } # Avoid inheriting test runner env on Windows
    $proc = Start-Process @param

    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $ready = $false
    $attempt = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($proc.HasExited) { break }
        $attempt++
        # Optional lightweight HTTP/HTTPS probe of '/'
        try {
            $probeUri = ("http://{0}:{1}/health" -f $serverIp, $Port)
            $probeParams = @{ Uri = $probeUri; UseBasicParsing = $true; Method = 'Get'; TimeoutSec = 3; ErrorAction = 'Stop' }
            $probe = Invoke-WebRequest @probeParams
            if ($probe.StatusCode -ge 200 -and $probe.StatusCode -lt 600) { $ready = $true; break }
        } catch {
            Write-Debug "HTTP probe failed (route initialization likely incomplete): $($_.Exception.Message)"
        }

        try {
            $probeUri = ("https://{0}:{1}/health" -f $serverIp, $Port)
            $probeParams = @{ Uri = $probeUri; UseBasicParsing = $true; Method = 'Get'; TimeoutSec = 3; ErrorAction = 'Stop' ; SkipCertificateCheck = $true }
            $probe = Invoke-WebRequest @probeParams
            if ($probe.StatusCode -ge 200 -and $probe.StatusCode -lt 600) { $ready = $true; break }
        } catch {
            Write-Debug "HTTPS probe failed (route initialization likely incomplete): $($_.Exception.Message)"
        }
        Start-Sleep -Milliseconds $HttpProbeDelayMs
    }
    $exited = $proc.HasExited
    if (-not $ready -and -not $exited) {
        Write-Warning "Example $Name not accepting connections on port $Port after timeout. Continuing; requests may fail."
    }
    if ($exited) {
        Write-Warning "Example $Name process exited early with code $($proc.ExitCode). Capturing logs."
        if (Test-Path $stdErr) { Write-Warning ("stderr: " + (Get-Content $stdErr -Raw)) }
        if (Test-Path $stdOut) { Write-Verbose ("stdout: " + (Get-Content $stdOut -Raw)) -Verbose }
    }

    # Heuristic: detect HTTPS usage if listener line includes cert/self-signed flags
    $usesHttps = $false
    if ($content -match 'Add-KrEndpoint[^\n]*-SelfSignedCert' -or $content -match 'Add-KrEndpoint[^\n]*-CertPath' -or $content -match 'Add-KrEndpoint[^\n]*-X509Certificate') {
        $usesHttps = $true
    }

    Start-Sleep -Seconds 2 # Allow some time for server to stabilize

    return [pscustomobject]@{
        Name = $Name
        Url = ("{0}://{1}:{2}" -f ($usesHttps ? 'https' : 'http'), $serverIp, $Port)
        Host = $serverIp
        Port = $Port
        TempPath = $tmp
        Process = $proc
        Content = $content
        StdOut = $stdOut
        StdErr = $stdErr
        ExitedEarly = $exited
        Ready = $ready
        ScriptDirectory = $scriptDir
        OriginalLocation = $originalLocation
        PushedLocation = $true
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
        if ($r -match '{\*') { continue }
        $url = Convert-RouteToUrl -Route $r -Port $Instance.Port
        $method = 'Get'
        $body = $null
        $headers = @{}
        if ($r -like '/input/{value}' -or $r -like '/{value}') { $url = $url -replace 'sample$', 'demoValue'; $method = 'Get' }

        if ($CustomInvokers -and $CustomInvokers.ContainsKey($r)) {
            & $CustomInvokers[$r] -Url $url -Port $Instance.Port -Route $r | Out-Null
            continue
        }

        Write-Host "Testing $($Instance.Name): $r -> $url ($method)" -ForegroundColor Cyan
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
    $text = if ($Yaml -is [string]) { $Yaml } else { ($Yaml | Out-String) }
    # Normalize numeric-per-line payload (observed in YAML response) to characters
    $lines = ($text -split "`r?`n") | Where-Object { $_ -ne '' }
    $digitLines = $lines | Where-Object { $_ -match '^[0-9]+$' }
    if ($digitLines.Count -gt 0 -and $digitLines.Count -ge ($lines.Count * 0.6)) {
        try {
            $chars = $digitLines | ForEach-Object { [char][int]$_ }
            $text = -join $chars
        } catch { Write-Debug "Failed to normalize numeric YAML lines: $($_.Exception.Message)" }
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
