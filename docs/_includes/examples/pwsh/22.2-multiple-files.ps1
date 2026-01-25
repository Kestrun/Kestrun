<#!
    22.2 multipart/form-data with multiple files under the same field name

    Client example (PowerShell):
        $Port = 5000
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Batch upload'),'note')
        $file1 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('file-1'))
        $file1.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file1,'files','one.txt')
        $file2 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('file-2'))
        $file2.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file2,'files','two.txt')
        $resp = $client.PostAsync("http://127.0.0.1:$Port/upload", $content).Result
        $resp.Content.ReadAsStringAsync().Result

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.2-multiple-files')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.2'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.2-multiple-files'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot
$options.ComputeSha256 = $true

Add-KrFormRoute -Pattern '/upload' -Options $options -ScriptBlock {
    $files = @($FormPayload.Files['files'])
    $result = [pscustomobject]@{
        count = $files.Count
        files = $files | ForEach-Object {
            [pscustomobject]@{
                fileName = $_.OriginalFileName
                length = $_.Length
                sha256 = $_.Sha256
            }
        }
    }
    Write-KrJsonResponse -InputObject $result -StatusCode 200
}

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
