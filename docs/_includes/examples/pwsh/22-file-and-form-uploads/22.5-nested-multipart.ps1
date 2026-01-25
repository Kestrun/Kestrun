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
        Remove-Item -Recurse -Force (Join-Path $PSScriptRoot 'uploads')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.5'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

$uploadRoot = Join-Path $PSScriptRoot 'uploads'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot
$options.Limits.MaxNestingDepth = 1

Add-KrFormRoute -Pattern '/nested' -Options $options -ScriptBlock {
    param($Form)
    $payload = $Form.Payload
    $outerParts = $payload.Parts
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
