# 9.1 Text & JSON Responses Demo
# Starts a Kestrun server and exposes routes demonstrating
# Write-KrTextResponse and Write-KrJsonResponse features.

New-KrServer -Name 'Responses 9.1' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5091' | Out-Null
Add-KrRuntimePowerShell | Out-Null

# Simple text
Add-KrMapRoute -Path '/ping' -Method GET -ScriptBlock {
    Write-KrTextResponse 'pong'
}

# Created text with custom content type
Add-KrMapRoute -Path '/created' -Method POST -ScriptBlock {
    Write-KrTextResponse -InputObject 'resource created' -StatusCode 201 -ContentType 'text/plain; charset=utf-8'
}

# JSON basic
Add-KrMapRoute -Path '/time' -Method GET -ScriptBlock {
    [pscustomobject]@{ utc = (Get-Date).ToUniversalTime(); version = 1 } | Write-KrJsonResponse -Compress
}

# JSON depth + pretty (no -Compress)
Add-KrMapRoute -Path '/config' -Method GET -ScriptBlock {
    $cfg = [pscustomobject]@{ name = 'demo'; nested = [pscustomobject]@{ a = 1; b = 2; c = @(1, 2, 3) } }
    $cfg | Write-KrJsonResponse -Depth 5
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.1 server running on http://127.0.0.1:5091'
