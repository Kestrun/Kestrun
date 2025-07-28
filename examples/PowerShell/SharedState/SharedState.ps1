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
    }
    else {
        # If the source module does not exist, import the installed Kestrun module
        # This is useful for running the script in a production environment where the module is installed
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
}
catch {
    # If the import fails, output an error message and exit
    # This ensures that the script does not continue running if the module cannot be loaded
    Write-Error "Failed to import Kestrun module: $_"
    exit 1
}

New-KrLogger  |
Set-KrMinimumLevel -Value Debug  |
Add-KrSinkFile -Path ".\logs\sharedState.log" -RollingInterval Hour |
Add-KrSinkConsole |
Register-KrLogger -SetAsDefault -Name "DefaultLogger"
# Seed a global counter (Visits) — injected as $Visits in every runspace
Set-KrSharedState  -Name 'Visits' -Value @{Count = 0 }
# Create the server
$server = New-KrServer -Name 'MyKestrunServer' |

# Listen on port 5000 (HTTP)
Add-KrListener -Port 5000 | Add-KrPowerShellRuntime |

Enable-KrConfiguration


# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /ps/show
#   • $Visits is already injected as a PS variable
#   • Just read and write it back in the response
# ─────────────────────────────────────────────────────────────────────────────
Add-KrMapRoute -Server $server -Verbs Get -Path '/ps/show' -ScriptBlock {
    # $Visits is available
    Write-KrTextResponse -inputObject "Visits so far: $($Visits.Count)" -statusCode 200
}

# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /ps/visit
#   • Increment $Visits directly
#   • Persist the new value back into the global store
# ─────────────────────────────────────────────────────────────────────────────
Add-KrMapRoute -Server $server -Verbs Get -Path '/ps/visit' -ScriptBlock {
    # increment the injected variable
    $Visits.Count++

    Write-KrTextResponse -inputObject "Incremented to $($Visits.Count)" -statusCode 200
}

# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /cs/show
#   • $Visits is already injected as a PS variable
#   • Just read and write it back in the response
# ─────────────────────────────────────────────────────────────────────────────
Add-KrMapRoute -Server $server -Verbs Get -Path '/cs/show' -Code @'
    // $Visits is available
    Context.Response.WriteTextResponse($"Visits so far: {Visits["Count"]}", 200);
'@ -Language CSharp

# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /cs/visit
#   • Increment $Visits directly
#   • Persist the new value back into the global store
# ─────────────────────────────────────────────────────────────────────────────
Add-KrMapRoute -Server $server -Verbs Get -Path '/cs/visit' -Code @'
    // increment the injected variable
    Visits["Count"] = ((int)Visits["Count"]) + 1;

    Context.Response.WriteTextResponse($"Incremented to {Visits["Count"]}", 200);
'@ -Language CSharp

Add-KrMapRoute -Server $server -Verbs Get -Path '/' -ScriptBlock {
    # $Visits is available
    Write-KrRedirectResponse -Url '/ps/show' -Message "Redirecting to /ps/show"
}


# Start the server (blocking)
Start-KrServer -Server $server
