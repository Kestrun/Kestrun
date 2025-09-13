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

$logger = New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\HtmlTemplate.log' -RollingInterval Hour |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -PassThru -SetAsDefault
# Seed a global counter (Visits) — injected as $Visits in every runspace
Set-KrSharedState -Name 'Visits' -Value @{Count = 0 }
# Create the server
$server = New-KrServer -Name 'Kestrun HtmlTemplate' -Logger $logger -PassThru

# Listen on port 5000 (HTTP)
Add-KrListener -Port 5000 -PassThru | Add-KrPowerShellRuntime -PassThru |

    Enable-KrConfiguration -PassThru


Add-KrHtmlTemplateRoute -Pattern '/status' -HtmlTemplatePath './Pages/status.html'

# Inject the global variable into the route
Add-KrMapRoute -Verbs Get -Pattern '/visit' -ScriptBlock {
    # increment the injected variable
    $Visits.Count++

    Write-KrTextResponse -InputObject "Incremented to $($Visits.Count)" -StatusCode 200
}


# Start the server (blocking)
Start-KrServer -Server $server
