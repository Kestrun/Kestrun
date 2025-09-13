# 9.8 Caching & Revalidation

New-KrServer -Name 'Responses 9.8' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5098' | Out-Null
Add-KrRuntimePowerShell | Out-Null

# Public cached 5m
Add-KrMapRoute -Path '/cache-public' -Method GET -ScriptBlock {
    Add-KrCacheResponse -Public -MaxAge 300
    Write-KrTextResponse 'cached for 5m'
}

# Private no-store
Add-KrMapRoute -Path '/cache-private' -Method GET -ScriptBlock {
    Add-KrCacheResponse -Private -NoStore
    Write-KrTextResponse 'no-store'
}

# ETag based on payload body
Add-KrMapRoute -Path '/etag' -Method GET -ScriptBlock {
    $payload = (Get-Date).ToUniversalTime().ToString('O')
    if (-not (Test-KrCacheRevalidation -Payload $payload)) {
        Write-KrTextResponse $payload
    }
}

# Explicit versioned resource
Add-KrMapRoute -Path '/versioned' -Method GET -ScriptBlock {
    if (-not (Test-KrCacheRevalidation -ETag 'v1' -LastModified (Get-Date '2024-01-01'))) {
        Write-KrTextResponse 'v1 payload'
    }
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.8 server running on http://127.0.0.1:5098'
