<#!
    22.3-Url-Encoded application/x-www-form-urlencoded forms

    Client example (PowerShell):
        $body = 'name=Kestrun&role=admin&role=maintainer'
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/form" -ContentType 'application/x-www-form-urlencoded' -Body $body

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.3-urlencoded')
#>
param(
    [int]$Port = $env:PORT ?? 5000
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.3'

Add-KrEndpoint -Port $Port | Out-Null

# Upload directory
$scriptName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
$uploadRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "kestrun-uploads-$scriptName"

# Opt-in: only multipart/form-data is enabled by default
New-KrFormPartRule -Name 'name' -Required |
    New-KrFormPartRule -Name 'role' |
    Add-KrFormOption -DefaultUploadPath $uploadRoot -AllowedRequestContentTypes 'application/x-www-form-urlencoded' |

    Add-KrFormRoute -Pattern '/form' -ScriptBlock {
        $fields = @{}
        foreach ($key in $FormPayload.Fields.Keys) {
            $fields[$key] = $FormPayload.Fields[$key]
        }
        Write-KrJsonResponse -InputObject @{ fields = $fields } -StatusCode 200
    }

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
