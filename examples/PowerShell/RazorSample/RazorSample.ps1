<#
.SYNOPSIS
    Kestrun PowerShell Example: Global Variable Usage
.DESCRIPTION
    This script demonstrates how to define, retrieve, and remove global variables
    in Kestrun, a PowerShell web server framework.
#>

try {
    # Get the path of the current script
    # This allows the script to be run from any location
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    # Determine the script path and Kestrun module path
    $powerShellExamplesPath = (Split-Path -Parent ($ScriptPath))
    # Determine the script path and Kestrun module path
    $examplesPath = (Split-Path -Parent ($powerShellExamplesPath))
    # Get the parent directory of the examples path
    # This is useful for locating the Kestrun module
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    # Construct the path to the Kestrun module
    # This assumes the Kestrun module is located in the src/PowerShell/Kestr
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"

    # Import Kestrun module (from source if present, otherwise the installed module)
    if (Test-Path $kestrunModulePath -PathType Leaf) {
        # Import the Kestrun module from the source path if it exists
        # This allows for development and testing of the module without needing to install it
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        # If the source module does not exist, import the installed Kestrun module
        # This is useful for running the script in a production environment where the module is installed
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    # If the import fails, output an error message and exit
    # This ensures that the script does not continue running if the module cannot be loaded
    Write-Error "Failed to import Kestrun module: $_"
    exit 1
}

New-KrLogger |
    Set-KrLoggerMinimumLevel -Values Debug |
    Add-KrSinkFile -Path '.\logs\razor.log' -RollingInterval Hour |
    Add-KrSinkConsole |
    Register-KrLogger -SetAsDefault -Name 'DefaultLogger'

# Create the server
New-KrServer -Name 'MyKestrunServer'

Set-KrServerOptions -DenyServerHeader

#|
Set-KrServerLimit -MaxConcurrentConnections 100 -MaxRequestBodySize 10485760 -MaxRequestHeaderCount 100 -KeepAliveTimeout 120
# Listen on port 5000 (HTTP)
Add-KrEndpoint -Port 5000
Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @(
    'text/plain',
    'text/css',
    'application/javascript',
    'application/json',
    'application/xml',
    'text/html'
)

Add-KrPowerShellRuntime
Add-KrPowerShellRazorPagesRuntime

Add-KrCorsPolicyMiddleware -Name 'AllowAll' -AllowAnyOrigin -AllowAnyMethod -AllowAnyHeader
Add-KrFileServerMiddleware -RequestPath '/assets' -EnableDirectoryBrowsing

Add-KrStaticFilesMiddleware -RequestPath '/static'


Enable-KrConfiguration


Add-KrMapRoute -Server $server -Verbs Get -Pattern '/ps/json' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Json Response'
    # Payload
    $payload = @{
        Body = 'Hello from PowerShell script! - Json Response'
        RequestQuery = $Context.Request.Query
        RequestHeaders = $Context.Request.Headers
        RequestMethod = $Context.Request.Method
        RequestPath = $Context.Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody = $Context.Request.Body
    }
    Write-KrJsonResponse -InputObject $payload -StatusCode 200
}
# Start the server (blocking)
Start-KrServer -Server $server
