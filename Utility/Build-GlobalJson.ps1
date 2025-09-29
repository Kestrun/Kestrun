[CmdletBinding()]
param (
    [Parameter()]
    [ValidateSet('8', '9', '10')]
    [string]$Version = '9'
)
$sdks = dotnet --list-sdks | Where-Object { $_ -match "^$Version\.0\." }
$sortedSdks = @($sdks | Sort-Object { [version]($_.Split()[0]) } -Descending)
$latestSdk = $sortedSdks[0].Split()[0]

if ($latestSdk) {
    $globalJson = @{
        sdk = @{ version = $latestSdk }
    } | ConvertTo-Json -Depth 3
    Set-Content -Path './global.json' -Values $globalJson
    Write-Output "📝 Created global.json with SDK version $latestSdk"
} else {
    Write-Output "⚠️ No $Version.0 SDK found."
}
