
<#
    Sample: Caching & Revalidation
    Purpose: Demonstrate cache-control and ETag/Last-Modified revalidation in a Kestrun server.
    File:    9.8-Caching.ps1
    Notes:   Shows public/private cache, ETag, and versioned resource routes.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.8'

# 3. Listener
Add-KrEndpoint -IPAddress $IPAddress -Port $Port

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration
Enable-KrConfiguration

# Public cached route
Add-KrMapRoute -Pattern '/cache-public' -Verbs GET -ScriptBlock {
    Add-KrCacheResponse -Public -MaxAge 300
    Write-KrTextResponse 'cached for 5m'
}

# Private no-store route
Add-KrMapRoute -Pattern '/cache-private' -Verbs GET -ScriptBlock {
    Add-KrCacheResponse -Private -NoStore
    Write-KrTextResponse 'no-store'
}

# ETag negotiation route
Add-KrMapRoute -Pattern '/etag' -Verbs GET -ScriptBlock {
    $payload = 'stable content for ETag demo'
    if (-not (Test-KrCacheRevalidation -Payload $payload)) {
        Write-KrTextResponse $payload
    }
}

# Explicit versioned resource route
Add-KrMapRoute -Pattern '/versioned' -Verbs GET -ScriptBlock {
    $payload = 'v1 payload'
    if (-not (Test-KrCacheRevalidation -Payload $payload -ETag 'v1' -LastModified (Get-Date '2024-01-01'))) {
        Write-KrTextResponse $payload
    }
}

# Start the server
Start-KrServer
