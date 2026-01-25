<#!
    22.4 multipart/mixed (ordered parts)

    Client example (PowerShell):
        $boundary = 'mixed-boundary'
        $body = @(
            "--$boundary",
            "Content-Type: text/plain",
            "",
            "first",
            "--$boundary",
            "Content-Type: application/json",
            "",
            '{"value":42}',
            "--$boundary--",
            ""
        ) -join "`r`n"
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/mixed" -ContentType "multipart/mixed; boundary=$boundary" -Body $body

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path $PSScriptRoot 'uploads')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.4'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

$uploadRoot = Join-Path $PSScriptRoot 'uploads'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot

Add-KrFormRoute -Pattern '/mixed' -Options $options -ScriptBlock {
    param($Form)
    $payload = $Form.Payload
    $contentTypes = $payload.Parts | ForEach-Object { $_.ContentType }
    Write-KrJsonResponse -InputObject @{ count = $payload.Parts.Count; contentTypes = $contentTypes } -StatusCode 200
}

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
