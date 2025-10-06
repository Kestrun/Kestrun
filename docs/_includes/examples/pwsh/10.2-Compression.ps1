<#
    Compression Demo Script
    Demonstrates response compression for multiple content types.
    Routes return sufficiently large payloads so you can observe the
    `Content-Encoding` header (gzip/brotli/deflate) when the client
    sends an appropriate `Accept-Encoding`.

    Try (PowerShell 7+):
        irm https://127.0.0.1:5000/text    -SkipCertificateCheck -MaximumRedirection 0 -Headers @{ 'Accept-Encoding'='gzip' } -Verbose
        (Invoke-WebRequest -Uri https://127.0.0.1:5000/json -SkipCertificateCheck -Headers @{ 'Accept-Encoding'='br, gzip' }).Headers.'Content-Encoding'

    NOTE: Very small responses may be excluded from compression depending
          on server heuristics; all payloads below are padded to be >1KB.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# 1. Logging (console only for clarity)
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Compression Demo'

# 3. Listener (HTTP + self-signed HTTPS)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert | Out-Null

# IMPORTANT: Define helper functions BEFORE adding the PowerShell runtime so that
# they are available inside the isolated runspaces used for route execution.
function _NewLargeBlock([string]$seed, [int]$repeat = 80) {
    return (($seed + ' ') * $repeat).Trim()
}


# 5. Compression middleware (cover common textual MIME types)
Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @('text/plain', 'text/html', 'application/json', 'application/xml', 'application/x-www-form-urlencoded')

# 6. Lock in baseline configuration
Enable-KrConfiguration

# /text   (plain text)
Add-KrMapRoute -Verbs Get -Pattern '/text' -ScriptBlock {
    $body = _NewLargeBlock 'PlainText payload demonstrating compression' 120
    Write-Verbose "[/text] length=$($body.Length)" -Verbose
    Write-KrTextResponse -InputObject $body -StatusCode 200
}

# /json   (JSON array)
Add-KrMapRoute -Verbs Get -Pattern '/json' -ScriptBlock {
    $items = 1..150 | ForEach-Object { [pscustomobject]@{ id = $_; message = 'Compression JSON sample record ' + $_ } }
    Write-Verbose "[/json] count=$($items.Count)" -Verbose
    Write-KrJsonResponse -InputObject $items -StatusCode 200
}

# /html   (HTML page)
Add-KrMapRoute -Verbs Get -Pattern '/html' -ScriptBlock {
    $paras = 1..40 | ForEach-Object { '<p>Lorem ipsum dolor sit amet (para ' + $_ + ')</p>' }
    $html = @"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Compression Demo</title></head>
<body>
<h1>HTML Compression Demo</h1>
$($paras -join "`n")
</body>
</html>
"@
    Write-KrHtmlResponse -Template $html -StatusCode 200
}

# /xml    (XML serialization of objects)
Add-KrMapRoute -Verbs Get -Pattern '/xml' -ScriptBlock {
    $data = 1..120 | ForEach-Object { [pscustomobject]@{ Index = $_; Text = 'Sample XML record ' + $_ } }
    Write-Verbose "[/xml] count=$($data.Count)" -Verbose
    Write-KrXmlResponse -InputObject $data -StatusCode 200
}

# /form   (url-encoded style body)
Add-KrMapRoute -Verbs Get -Pattern '/form' -ScriptBlock {
    $pairs = 1..200 | ForEach-Object { "k$($_)=value$($_)" }
    $payload = ($pairs -join '&')
    Write-Verbose "[/form] pairs=$($pairs.Count) length=$($payload.Length)" -Verbose
    $Context.Response.ContentType = 'application/x-www-form-urlencoded'
    $Context.Response.Body = $payload
    $Context.Response.StatusCode = 200
}

# /raw-nocompress  (large payload explicitly excluded from compression)
Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
        Pattern = '/raw-nocompress'
        HttpVerbs = 'Get'
        ScriptCode = @{
            Code = {
                $body = _NewLargeBlock 'This route intentionally disables response compression.' 150
                Write-Verbose "[/raw-nocompress] length=$($body.Length)" -Verbose
                Write-KrTextResponse -InputObject $body -StatusCode 200 -ContentType 'text/plain'
            }
            Language = 'PowerShell'
        }
        DisableResponseCompression = $true
    })

# /info   (reflect Accept-Encoding to show negotiation)
Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock {
    $ae = Get-KrRequestHeader -Name 'Accept-Encoding'
    $body = [pscustomobject]@{
        AcceptEncoding = $ae
        Tip = 'Use -Headers @{"Accept-Encoding"="gzip"} (or br,deflate) to see compressed responses.'
        Routes = '/text,/json,/html,/xml,/form,/raw-nocompress'
        NonCompressedExample = '/raw-nocompress'
    }
    Write-KrJsonResponse -InputObject $body -StatusCode 200
}

# Start server
Start-KrServer -CloseLogsOnExit

# Close default logger when process exits
Close-KrLogger
