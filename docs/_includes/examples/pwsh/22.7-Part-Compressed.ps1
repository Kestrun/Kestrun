<#!
    22.7 Upload with part-level compression (optional feature)

    Client example (PowerShell):
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $raw = [System.Text.Encoding]::UTF8.GetBytes('compressed-part')
        $ms = [System.IO.MemoryStream]::new()
        $gzip = [System.IO.Compression.GZipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress, $true)
        $gzip.Write($raw, 0, $raw.Length)
        $gzip.Dispose()
        $compressed = $ms.ToArray()
        $part = [System.Net.Http.ByteArrayContent]::new($compressed)
        $part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $part.Headers.ContentEncoding.Add('gzip')
        $content.Add($part,'file','payload.txt')
        $resp = $client.PostAsync("http://127.0.0.1:$Port/part-compressed", $content).Result
        $resp.Content.ReadAsStringAsync().Result

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.7-part-compressed')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.7'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null
# Upload directory
$scriptName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
$uploadRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "kestrun-uploads-$scriptName"
$maxDecompressedBytesPerPart = 1MB

# Add Rules
New-KrFormPartRule -Name 'file' -Required -AllowOnlyOne -AllowedContentTypes 'text/plain' |
    Add-KrFormOption -DefaultUploadPath $uploadRoot -ComputeSha256 -EnablePartDecompression -MaxDecompressedBytesPerPart $maxDecompressedBytesPerPart |
    Add-KrFormRoute -Pattern '/part-compressed' -ScriptBlock {
        $file = $FormPayload.Files['file'][0]
        Write-KrJsonResponse -InputObject @{ fileName = $file.OriginalFileName; length = $file.Length; sha256 = $file.Sha256 } -StatusCode 200
    }

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
