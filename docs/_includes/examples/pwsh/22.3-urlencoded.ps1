<#!
    22.3 application/x-www-form-urlencoded forms

    Client example (PowerShell):
        $body = 'name=Kestrun&role=admin&role=maintainer'
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/form" -ContentType 'application/x-www-form-urlencoded' -Body $body

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.3-urlencoded')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.3'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.3-urlencoded'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot

Add-KrFormRoute -Pattern '/form' -Options $options -ScriptBlock {
    $fields = @{}
    foreach ($key in $FormPayload.Fields.Keys) {
        $fields[$key] = $FormPayload.Fields[$key]
    }
    Write-KrJsonResponse -InputObject @{ fields = $fields } -StatusCode 200
}

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
