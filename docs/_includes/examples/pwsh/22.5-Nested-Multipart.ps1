<#!
    22.5 nested multipart/mixed (one level)

    Client example (PowerShell):
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $innerBody = @(
            "--$inner",
            "Content-Type: text/plain",
            "",
            "inner-1",
            "--$inner",
            "Content-Type: application/json",
            "",
            '{"nested":true}',
            "--$inner--",
            ""
        ) -join "`r`n"
        $outerBody = @(
            "--$outer",
            "Content-Type: application/json",
            "",
            '{"stage":"outer"}',
            "--$outer",
            "Content-Type: multipart/mixed; boundary=$inner",
            "",
            $innerBody,
            "--$outer--",
            ""
        ) -join "`r`n"
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $outerBody

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.5-nested-multipart')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.5'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# Upload directory
$scriptName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
$uploadRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "kestrun-uploads-$scriptName"

# Add Rules
# Note: nested multipart is parsed as ordered parts; rules apply when a part includes a Content-Disposition name.
New-KrFormPartRule -Name 'outer' -MaxBytes 1024 |
    New-KrFormPartRule -Name 'nested' -MaxBytes (1024 * 1024) |
    Add-KrFormOption -DefaultUploadPath $uploadRoot -AllowedRequestContentTypes 'multipart/mixed' -MaxNestingDepth 1 |
    Add-KrFormRoute -Pattern '/nested' -ScriptBlock {
        $outerParts = $FormPayload.Parts
        $nestedSummary = @()
        foreach ($part in $outerParts) {
            if ($null -ne $part.NestedPayload) {
                $nestedSummary += [pscustomobject]@{
                    outerContentType = $part.ContentType
                    nestedCount = $part.NestedPayload.Parts.Count
                }
            }
        }
        Write-KrJsonResponse -InputObject @{ outerCount = $outerParts.Count; nested = $nestedSummary } -StatusCode 200
    }

Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
