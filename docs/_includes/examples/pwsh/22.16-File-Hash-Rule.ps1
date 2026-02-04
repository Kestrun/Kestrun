<#
    22.16 File hash upload (rules)

    Client example (PowerShell):
        $filePath = Join-Path $env:TEMP 'upload.bin'
        [System.IO.File]::WriteAllBytes($filePath, (1..255))
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $stream = [System.IO.File]::OpenRead($filePath)
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
        $content.Add($fileContent, 'file', (Split-Path $filePath -Leaf))
        $resp = $client.PostAsync("http://127.0.0.1:$Port/upload-hash", $content).Result
        $resp.Content.ReadAsStringAsync().Result
        $stream.Dispose()
        $content.Dispose()
        $client.Dispose()

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.16-file-hash-rule')
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingBrokenHashAlgorithms', '')]
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.16'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# Upload directory
$scriptName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
$uploadRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "kestrun-uploads-$scriptName"

# Maximum allowed size for uploaded files and request body.
# 600MB is large enough for most test files while preventing excessively large uploads in this example.
$maxUploadFileSize = 600MB

# Add rules and route
New-KrFormPartRule -Name 'file' -Required -AllowedContentTypes 'application/octet-stream' -AllowOnlyOne -MaxBytes $maxUploadFileSize |
    Add-KrFormOption -DefaultUploadPath $uploadRoot -MaxRequestBodyBytes $maxUploadFileSize -MaxPartBodyBytes $maxUploadFileSize -MaxDecompressedBytesPerPart $maxUploadFileSize |
    Add-KrFormRoute -Pattern '/upload-hash' -ScriptBlock {
        if (-not $FormPayload.Files.ContainsKey('file')) {
            Write-KrJsonResponse -InputObject @{ error = 'file part missing' } -StatusCode 400
            return
        }

        $file = $FormPayload.Files['file'] | Select-Object -First 1
        $sha1 = (Get-FileHash -Algorithm SHA1 -Path $file.TempPath).Hash
        $sha256 = (Get-FileHash -Algorithm SHA256 -Path $file.TempPath).Hash
        $sha384 = (Get-FileHash -Algorithm SHA384 -Path $file.TempPath).Hash
        $sha512 = (Get-FileHash -Algorithm SHA512 -Path $file.TempPath).Hash
        $md5 = (Get-FileHash -Algorithm MD5 -Path $file.TempPath).Hash

        $response = [pscustomobject]@{
            fileName = $file.OriginalFileName
            contentType = $file.ContentType
            size = $file.Length
            sha1 = $sha1
            sha256 = $sha256
            sha384 = $sha384
            sha512 = $sha512
            md5 = $md5
        }

        Write-KrJsonResponse -InputObject $response -StatusCode 200
    }

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
