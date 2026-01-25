<#!
    22.3 application/x-www-form-urlencoded forms

    Client example (PowerShell):
        $body = 'name=Kestrun&role=admin&role=maintainer'
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/form" -ContentType 'application/x-www-form-urlencoded' -Body $body

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

New-KrServer -Name 'Forms 22.3'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

$uploadRoot = Join-Path $PSScriptRoot 'uploads'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot

Add-KrFormRoute -Pattern '/form' -Options $options -ScriptBlock {
    param($Form)
    $payload = $Form.Payload
    $fields = @{}
    foreach ($key in $payload.Fields.Keys) {
        $fields[$key] = $payload.Fields[$key]
    }
    Write-KrJsonResponse -InputObject @{ fields = $fields } -StatusCode 200
}

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
