<#
    .SYNOPSIS
        Sends an HTTP request to a Kestrun server over various transport mechanisms (TCP, Named Pipe, Unix Socket).
    .DESCRIPTION
        This function allows sending HTTP requests to a Kestrun server using different transport methods, including TCP, Named Pipe, and Unix Socket.
        It supports various HTTP methods, custom headers, request bodies, and response handling options.
    .PARAMETER NamedPipeName
        The name of the named pipe to connect to. This parameter is mandatory when using the NamedPipe transport.
    .PARAMETER UnixSocketPath
        The file system path to the Unix domain socket. This parameter is mandatory when using the UnixSocket transport.
    .PARAMETER Uri
        The base URI of the Kestrun server. This parameter is mandatory when using the Tcp transport.
    .PARAMETER Method
        The HTTP method to use for the request (e.g., GET, POST, PUT, DELETE). The default is GET.
    .PARAMETER Path
        The request target path (e.g., '/api/resource'). Defaults to '/'.
    .PARAMETER Body
        The request body, which can be a string, byte array, or object (which will be serialized to JSON).
    .PARAMETER InFile
        The path to a file whose contents will be uploaded as the request body.
    .PARAMETER ContentType
        The content type of the request body (e.g., 'application/json').
    .PARAMETER Headers
        A hashtable of additional headers to include in the request.
    .PARAMETER UserAgent
        The User-Agent header value. Defaults to 'PowerShell/7 Kestrun-InvokeKrWebRequest'.
    .PARAMETER Accept
        The Accept header value. Defaults to '*/*'.
    .PARAMETER SkipCertificateCheck
        If specified, SSL certificate errors will be ignored (useful for self-signed certificates).
    .PARAMETER WebSession
        A hashtable containing a CookieContainer for managing cookies across requests.
    .PARAMETER SessionVariable
        The name of a variable to store the web session (cookies) for reuse in subsequent requests
    .PARAMETER DisallowAutoRedirect
        If specified, automatic redirection will be disabled.
    .PARAMETER MaximumRedirection
        The maximum number of automatic redirections to follow. Defaults to 50.
    .PARAMETER Credential
        The credentials to use for server authentication.
    .PARAMETER UseDefaultCredentials
        If specified, the default system credentials will be used for server authentication.
    .PARAMETER Proxy
        The URI of the proxy server to use for the request.
    .PARAMETER ProxyCredential
        The credentials to use for proxy authentication.
    .PARAMETER ProxyUseDefaultCredentials
        If specified, the default system credentials will be used for proxy authentication.
    .PARAMETER TimeoutSec
        The request timeout in seconds. Defaults to 100 seconds.
    .PARAMETER OutFile
        If specified, the response body will be saved to the given file path.
    .PARAMETER AsString
        If specified, the response body will be returned as a string. Otherwise, it will attempt to parse JSON if applicable.
    .PARAMETER PassThru
        If specified, the raw HttpResponseMessage will be returned.
    .EXAMPLE
        Invoke-KrWebRequest -Uri 'http://localhost:5000' -Method 'GET' -Path '/api/resource'
        Sends a GET request to the specified Kestrun server URI and path.
    .EXAMPLE
        Invoke-KrWebRequest -NamedPipeName 'MyNamedPipe' -Method 'POST' -Path '/api/resource' -Body @{ name = 'value' } -ContentType 'application/json'
        Sends a POST request with a JSON body to the Kestrun server over a named pipe.
    .EXAMPLE
        Invoke-KrWebRequest -UnixSocketPath '/var/run/kestrun.sock' -Method 'GET' -Path '/api/resource' -OutFile 'response.json'
        Sends a GET request to the Kestrun server over a Unix socket and saves the response body to a file.
    .NOTES
        This function requires the Kestrun.Net.dll assembly to be available in the same directory or a specified path.
        It is designed to work with Kestrun servers but can be adapted for other HTTP servers as needed.
#>
function Invoke-KrWebRequest {
    [CmdletBinding(DefaultParameterSetName = 'Tcp')]
    param(
        # Transport (pick one)
        [Parameter(Mandatory, ParameterSetName = 'NamedPipe')]
        [string]$NamedPipeName,

        [Parameter(Mandatory, ParameterSetName = 'UnixSocket')]
        [string]$UnixSocketPath,

        [Parameter(Mandatory, ParameterSetName = 'Tcp')]
        [uri]$Uri,

        # Request
        [ValidateSet('GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS', 'TRACE')]
        [string]$Method = 'GET',
        [string]$Path = '/',
        [object]$Body,
        [string]$InFile,
        [string]$ContentType,
        [hashtable]$Headers,
        [string]$UserAgent = 'PowerShell/7 Kestrun-InvokeKrWebRequest',
        [string]$Accept = '*/*',
        [int]$TimeoutSec = 100,
        [switch]$SkipCertificateCheck,

        # Web session (cookies)
        [Hashtable]$WebSession,              # { CookieContainer = <System.Net.CookieContainer> }
        [string]$SessionVariable,

        # Redirects
        [switch]$DisallowAutoRedirect,
        [int]$MaximumRedirection = 50,

        # Auth (server)
        [pscredential]$Credential,
        [switch]$UseDefaultCredentials,

        # Proxy
        [uri]$Proxy,
        [pscredential]$ProxyCredential,
        [switch]$ProxyUseDefaultCredentials,

        # Output
        [string]$OutFile,
        [switch]$AsString,
        [switch]$PassThru
    )

    # ensure DLL loaded (adjust path if needed)
    if (-not ([Type]::GetType('Kestrun.Client.KrHttpClientFactory, Kestrun.Net', $false))) {
        $try1 = Join-Path $PSScriptRoot '../lib/net8.0/Kestrun.Net.dll'
        $try2 = Join-Path $PSScriptRoot 'Kestrun.Net.dll'
        foreach ($p in @($try1, $try2)) {
            $rp = Resolve-Path -EA SilentlyContinue -LiteralPath $p
            if ($rp) { Add-Type -Path $rp.Path; break }
        }
    }

    # build options for the handler
    $cookieContainer = $null
    if ($WebSession -and $WebSession.ContainsKey('CookieContainer')) {
        $cookieContainer = $WebSession['CookieContainer']
    } else {
        # make a fresh cookie container if caller asked for a session via -SessionVariable
        if ($SessionVariable) { $cookieContainer = [System.Net.CookieContainer]::new() }
    }

    $opts = [Kestrun.Client.KrHttpClientOptions]::new()
    $opts.Timeout = [TimeSpan]::FromSeconds([Math]::Max(1, $TimeoutSec))
    $opts.IgnoreCertErrors = $SkipCertificateCheck.IsPresent
    $opts.Cookies = $cookieContainer
    $opts.AllowAutoRedirect = -not $DisallowAutoRedirect.IsPresent
    $opts.MaxAutomaticRedirections = [Math]::Max(1, $MaximumRedirection)

    if ($UseDefaultCredentials) { $opts.UseDefaultCredentials = $true }
    elseif ($Credential) {
        $opts.Credentials = $Credential.GetNetworkCredential()
    }

    if ($Proxy) {
        $webProxy = [System.Net.WebProxy]::new($Proxy)
        if ($ProxyUseDefaultCredentials) { $webProxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials }
        elseif ($ProxyCredential) { $webProxy.Credentials = $ProxyCredential.GetNetworkCredential() }
        $opts.Proxy = $webProxy
        $opts.UseProxy = $true
        $opts.ProxyUseDefaultCredentials = $ProxyUseDefaultCredentials.IsPresent
    }

    # cache key (vary by transport + timeout + TLS flag + redirect + session + proxy/auth)
    $sessionKey = if ($cookieContainer) { $cookieContainer.GetHashCode() } else { 0 }
    $authKey = @(
        $UseDefaultCredentials.IsPresent,
        [string]$Credential?.UserName,
        [string]$Proxy,
        $ProxyUseDefaultCredentials.IsPresent,
        [string]$ProxyCredential?.UserName,
        (-not $DisallowAutoRedirect.IsPresent),
        $MaximumRedirection
    ) -join '|'

    if (-not $script:__KrIwrClients) { $script:__KrIwrClients = @{} }
    $cacheKey = switch ($PSCmdlet.ParameterSetName) {
        'NamedPipe' { "pipe::$NamedPipeName::$($SkipCertificateCheck.IsPresent)::$TimeoutSec::$sessionKey::$authKey" }
        'UnixSocket' { "uds::$UnixSocketPath::$($SkipCertificateCheck.IsPresent)::$TimeoutSec::$sessionKey::$authKey" }
        'Tcp' { "tcp::$($Uri.AbsoluteUri)::$($SkipCertificateCheck.IsPresent)::$TimeoutSec::$sessionKey::$authKey" }
    }

    if (-not $script:__KrIwrClients.ContainsKey($cacheKey)) {
        $client = switch ($PSCmdlet.ParameterSetName) {
            'NamedPipe' { [Kestrun.Client.KrHttpClientFactory]::CreateNamedPipeClient($NamedPipeName, $opts) }
            'UnixSocket' { [Kestrun.Client.KrHttpClientFactory]::CreateUnixSocketClient($UnixSocketPath, $opts) }
            'Tcp' { [Kestrun.Client.KrHttpClientFactory]::CreateTcpClient($Uri, $opts) }
        }
        $script:__KrIwrClients[$cacheKey] = $client
    } else {
        $client = $script:__KrIwrClients[$cacheKey]
    }

    # Build request URI
    $target = if ($PSCmdlet.ParameterSetName -eq 'Tcp') {
        if ($Path) { [Uri]::new($client.BaseAddress, $Path) } else { $client.BaseAddress }
    } else {
        [Uri]::new(($Path.StartsWith('/') ? $Path : "/$Path"), [System.UriKind]::Relative)
    }

    # Build request
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $target)
    if ($UserAgent) { $null = $req.Headers.TryAddWithoutValidation('User-Agent', $UserAgent) }
    if ($Accept) { $null = $req.Headers.TryAddWithoutValidation('Accept', $Accept) }
    foreach ($k in ($Headers?.Keys ?? @())) { $null = $req.Headers.TryAddWithoutValidation([string]$k, [string]$Headers[$k]) }

    # Body / InFile
    if ($InFile) {
        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $InFile))
        $content = [System.Net.Http.ByteArrayContent]::new($bytes)
        if ($ContentType) { $content.Headers.ContentType = $ContentType }
        $req.Content = $content
    } elseif ($PSBoundParameters.ContainsKey('Body')) {
        switch ($Body) {
            { $_ -is [string] } {
                $ctype = $ContentType; if (-not $ctype) { $ctype = 'text/plain; charset=utf-8' }
                $req.Content = [System.Net.Http.StringContent]::new([string]$Body, [System.Text.Encoding]::UTF8, $ctype); break
            }
            { $_ -is [byte[]] } {
                $req.Content = [System.Net.Http.ByteArrayContent]::new([byte[]]$Body)
                if ($ContentType) { $req.Content.Headers.ContentType = $ContentType }
                break
            }
            default {
                $json = $Body | ConvertTo-Json -Depth 32 -Compress
                $req.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, ($ContentType ?? 'application/json'))
            }
        }
    }

    # Persist session if requested
    if ($SessionVariable) {
        if (-not $cookieContainer) { $cookieContainer = [System.Net.CookieContainer]::new() }
        # (Cookies are already in handler; we just hand the container out)
        Set-Variable -Name $SessionVariable -Scope 1 -Value @{ CookieContainer = $cookieContainer }
    }

    # ---- Send (streaming if -OutFile) ----
    if ($OutFile) {
        # Build a fresh request for streaming (do NOT reuse across paths)
        $streamReq = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $target)
        # clone headers
        if ($UserAgent) { $null = $streamReq.Headers.TryAddWithoutValidation('User-Agent', $UserAgent) }
        if ($Accept) { $null = $streamReq.Headers.TryAddWithoutValidation('Accept', $Accept) }
        foreach ($h in ($Headers?.Keys ?? @())) { $null = $streamReq.Headers.TryAddWithoutValidation([string]$h, [string]$Headers[$h]) }
        # clone content if present
        if ($req.Content) {
            $bytesForClone = $req.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            $streamReq.Content = [System.Net.Http.ByteArrayContent]::new($bytesForClone)
            foreach ($ch in $req.Content.Headers) { $null = $streamReq.Content.Headers.TryAddWithoutValidation($ch.Key, ($ch.Value -join ', ')) }
        }

        try {
            $outPath = (Resolve-Path -LiteralPath $OutFile).Path
            [Kestrun.Client.KrHttpDownloads]::DownloadToFileAsync($client, $streamReq, $outPath, $false).GetAwaiter().GetResult() | Out-Null

            # hand back the session cookie container if requested
            if ($SessionVariable) {
                if (-not $cookieContainer) { $cookieContainer = [System.Net.CookieContainer]::new() }
                Set-Variable -Name $SessionVariable -Scope 1 -Value @{ CookieContainer = $cookieContainer }
            }

            return [pscustomobject]@{
                StatusCode = 200
                StatusDescription = 'OK'
                Headers = $null
                RawContent = $null
                Content = $null
                BaseResponse = $null
                SavedTo = $outPath
            }
        } finally {
            $streamReq.Dispose()
            if ($req) { $req.Dispose() } # dispose the original builder too
        }
    }

    # ---- Non-file responses (beware of big bodies) ----
    # Standard send for non-OutFile cases; okay for JSON/text where you expect small/medium sizes.
    try {
        $res = $client.SendAsync($req).GetAwaiter().GetResult()
    } finally {
        $req.Dispose()
    }

    if ($PassThru) { return $res }

    $bytes = $res.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    $ctype = $res.Content.Headers.ContentType?.MediaType

    # session handoff after request completes
    if ($SessionVariable) {
        if (-not $cookieContainer) { $cookieContainer = [System.Net.CookieContainer]::new() }
        Set-Variable -Name $SessionVariable -Scope 1 -Value @{ CookieContainer = $cookieContainer }
    }

    [pscustomobject]@{
        StatusCode = [int]$res.StatusCode
        StatusDescription = $res.ReasonPhrase
        Headers = $res.Headers
        RawContent = $text
        Content = if ($ctype -and $ctype -like 'application/json*') { try { $text | ConvertFrom-Json -Depth 32 } catch { $text } } else { $text }
        BaseResponse = $res
        SavedTo = $null
    }
}
