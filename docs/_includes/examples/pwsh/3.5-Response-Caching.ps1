<#
    Sample Kestrun Server demonstrating response caching and conditional requests (ETag / Last-Modified).
    Shows how to configure response caching limits and a simple route that returns 304 when unchanged.
    FileName: 3.5-Response-Caching.ps1
#>

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
# Create a new Kestrun server
New-KrServer -Name "Simple Server"
# 4. PowerShell runtime
Add-KrPowerShellRuntime
# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add a file server with browsing enabled
Add-KrFileServer -RequestPath '/' -RootPath '.\Assets\wwwroot' -EnableDirectoryBrowsing -ContentTypeMap $map
# Add response caching with a 10MB size limit, 64KB max body size, case-sensitive paths,
# a shared max age of 100 seconds, and public cacheability.
Add-KrResponseCaching -SizeLimit 10485760 -MaximumBodySize 65536 -UseCaseSensitivePaths -SharedMaxAge 100 -Public -MaxAge 100

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a route that demonstrates response caching with ETag and Last-Modified support
Add-KrMapRoute -Verbs Get -Pattern '/cachetest' -ScriptBlock {
    $payload = "This is a cached response."  # keep it stable if you want 304s
    $etag = '"' + ([Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($payload))).ToLower()) + '"'
    # Check for conditional request; if a 304 was sent, just return
    if ((Test-KrCacheRevalidation -Payload $payload -ETag $etag -LastModified (Get-Date '2023-01-01'))) {
        return
    }
    # Fresh response
    Write-KrTextResponse -InputObject $payload -StatusCode 200
}
# Start the server asynchronously
Start-KrServer
