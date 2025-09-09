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
    .PARAMETER TimeoutSec
        The request timeout in seconds. Defaults to 100 seconds.
    .PARAMETER OutFile
        If specified, the response body will be saved to the given file path.
    .PARAMETER AsString
        If specified, the response body will be returned as a string. Otherwise, it will attempt to parse JSON if applicable.
    .PARAMETER PassThru
        If specified, the raw HttpResponseMessage will be returned.
#>
function Invoke-KrWebRequest {
    [CmdletBinding(DefaultParameterSetName = 'Tcp')]
    param(
        # --- Choose ONE transport ---
        [Parameter(Mandatory, ParameterSetName = 'NamedPipe')]
        [string]$NamedPipeName,

        [Parameter(Mandatory, ParameterSetName = 'UnixSocket')]
        [string]$UnixSocketPath,

        [Parameter(Mandatory, ParameterSetName = 'Tcp')]
        [uri]$Uri,

        # --- Request ---
        [ValidateSet('GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS', 'TRACE')]
        [string]$Method = 'GET',

        [string]$Path = '/',                 # request target e.g. '/pipe?x=1'
        [object]$Body,                       # string | byte[] | object (JSON)
        [string]$InFile,                     # upload file as body (binary)
        [string]$ContentType,                # e.g. 'application/json'
        [hashtable]$Headers,
        [string]$UserAgent = 'PowerShell/7 Kestrun-InvokeKrWebRequest',
        [string]$Accept = '*/*',
        [int]$TimeoutSec = 100,
        [switch]$SkipCertificateCheck,       # ignore TLS cert errors (all transports)

        # --- Output ---
        [string]$OutFile,                    # save response
        [switch]$AsString,                   # return body as string (else try JSON parse)
        [switch]$PassThru                    # return raw HttpResponseMessage
    )

    if (-not $script:__KrIwrClients) { $script:__KrIwrClients = @{} }

    # cache key includes SkipCertificateCheck (different handlers)
    $cacheKey = switch ($PSCmdlet.ParameterSetName) {
        'NamedPipe' { "pipe::$NamedPipeName::$($SkipCertificateCheck.IsPresent)::$TimeoutSec" }
        'UnixSocket' { "uds::$UnixSocketPath::$($SkipCertificateCheck.IsPresent)::$TimeoutSec" }
        'Tcp' { "tcp::$($Uri.AbsoluteUri)::$($SkipCertificateCheck.IsPresent)::$TimeoutSec" }
    }

    if (-not $script:__KrIwrClients.ContainsKey($cacheKey)) {
        $timeout = [TimeSpan]::FromSeconds([Math]::Max(1, $TimeoutSec))
        $client = switch ($PSCmdlet.ParameterSetName) {
            'NamedPipe' { [Kestrun.Client.KrHttpClientFactory]::CreateNamedPipeClient($NamedPipeName, $timeout, $SkipCertificateCheck.IsPresent) }
            'UnixSocket' { [Kestrun.Client.KrHttpClientFactory]::CreateUnixSocketClient($UnixSocketPath, $timeout, $SkipCertificateCheck.IsPresent) }
            'Tcp' { [Kestrun.Client.KrHttpClientFactory]::CreateTcpClient($Uri, $timeout, $SkipCertificateCheck.IsPresent) }
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
    foreach ($k in ($Headers?.Keys ?? @())) {
        $null = $req.Headers.TryAddWithoutValidation([string]$k, [string]$Headers[$k])
    }

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

    # Send
    $res = $client.SendAsync($req).GetAwaiter().GetResult()
    if ($PassThru) { return $res }

    # Materialize response
    $bytes = $res.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()

    if ($OutFile) {
        [System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath $OutFile), $bytes)
    }

    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    $ctype = $res.Content.Headers.ContentType?.MediaType

    # IWR-like result object
    [pscustomobject]@{
        StatusCode = [int]$res.StatusCode
        StatusDescription = $res.ReasonPhrase
        Headers = $res.Headers
        RawContent = $text
        Content = if ($ctype -and $ctype -like 'application/json*') { try { $text | ConvertFrom-Json -Depth 32 } catch { $text } } else { $text }
        BaseResponse = $res
        SavedTo = if ($OutFile) { (Resolve-Path -LiteralPath $OutFile).Path } else { $null }
    }
}
