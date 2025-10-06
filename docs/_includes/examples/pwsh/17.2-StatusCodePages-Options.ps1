#
# Sample: Status Code Pages with Options
# Demonstrates configuring StatusCodePages using options-like switches (template/redirect/re-exec)
# FileName: 17.2-StatusCodePages-Options.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot

New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create server and listener
New-KrServer -Name 'Status Code Pages with Options'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Choose ONE of the following blocks (uncomment the one you want)

# 1) Default (plain text)
Enable-KrStatusCodePage

# 2) Template body format (HTML with placeholder)
# $html = Get-Content -Path (Join-Path $PSScriptRoot 'Assets\wwwroot\statuscodepages\error-options.html') -Raw
# Enable-KrStatusCodePage -ContentType 'text/html; charset=utf-8' -BodyFormat $html

# 3) Redirects to /error/{status}
# Enable-KrStatusCodePage -LocationFormat '/error/{0}'

# 4) Re-execute pipeline at /errors/{status}
# Enable-KrStatusCodePage -PathFormat '/errors/{0}' -QueryFormat 'originalPath={0}'

# Commit configuration
Enable-KrConfiguration

# Demo route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrTextResponse -InputObject 'hello' -StatusCode 200
}

# Trigger routes
Add-KrMapRoute -Verbs Get -Pattern '/notfound' -ScriptBlock { Write-KrStatusResponse -StatusCode 404 }
Add-KrMapRoute -Verbs Get -Pattern '/error' -ScriptBlock { Write-KrStatusResponse -StatusCode 500 }
Add-KrMapRoute -Verbs Get -Pattern '/forbidden' -ScriptBlock { Write-KrStatusResponse -StatusCode 403 }
Add-KrMapRoute -Verbs Get -Pattern '/unauthorized' -ScriptBlock { Write-KrStatusResponse -StatusCode 401 }

Start-KrServer -CloseLogsOnExit
