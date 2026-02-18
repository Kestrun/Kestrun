<#
    15.11 Request Decompression Demo

    Demonstrates request-level decompression using RequestDecompression middleware.

    Key idea:
      - Client compresses the entire request body (Content-Encoding: gzip)
      - Middleware decompresses before Kestrun reads/parses the body (e.g. JSON)

    Try (PowerShell 7+):
        pwsh .\docs\_includes\examples\pwsh\15.11-Request-Decompression.ps1

        $payloadObject = @{
            message = (('hello ' * 20000).Trim())
            count   = 1
            items   = 1..2000
            meta    = @{ note = ('x' * 20000) }
        }
        $payload = $payloadObject | ConvertTo-Json -Compress -Depth 6
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $compressed = New-Object System.IO.MemoryStream
        $gzip = [System.IO.Compression.GZipStream]::new($compressed, [System.IO.Compression.CompressionMode]::Compress, $true)
        $gzip.Write($bytes, 0, $bytes.Length)
        $gzip.Dispose()

        Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:5000/api/echo" \
          -ContentType 'application/json' \
          -Headers @{ 'Content-Encoding' = 'gzip' } \
          -Body $compressed.ToArray()
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

Initialize-KrRoot -Path $PSScriptRoot

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Request Decompression Demo'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# Enable request-level decompression (entire body compressed; Content-Encoding: gzip)
Add-KrRequestDecompressionMiddleware -AllowedEncoding gzip

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    Write-KrTextResponse -InputObject 'POST /api/echo with Content-Encoding: gzip (JSON body) to see request decompression in action.' -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/api/echo' -ScriptBlock {
    $raw = Get-KrRequestBody -Raw
    $receivedBytes = [System.Text.Encoding]::UTF8.GetByteCount($raw)

    $messageStartsWith = ''
    $receivedCount = $null
    try {
        $body = $raw | ConvertFrom-Json -AsHashtable
        $messageText = [string]($body.message ?? '')
        $messageStartsWith = if ($messageText.Length -ge 5) { $messageText.Substring(0, 5) } else { $messageText }
        $receivedCount = $body.count
    } catch {
        $messageStartsWith = ''
        $receivedCount = $null
    }

    Write-KrJsonResponse -InputObject @{
        ok = $true
        receivedBytes = $receivedBytes
        receivedCount = $receivedCount
        messageStartsWith = $messageStartsWith
    } -StatusCode 200
}

Add-KrMapRoute -Verbs Post -Pattern '/api/text' -ScriptBlock {
    $raw = Get-KrRequestBody -Raw
    $receivedBytes = [System.Text.Encoding]::UTF8.GetByteCount($raw)

    Write-KrJsonResponse -InputObject @{
        ok = $true
        receivedBytes = $receivedBytes
        contentType = $Context.Request.ContentType
    } -StatusCode 200
}

Start-KrServer -CloseLogsOnExit
