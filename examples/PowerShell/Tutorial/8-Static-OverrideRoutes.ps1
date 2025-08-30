<#
    Sample Kestrun Server on how to use static routes.
    These examples demonstrate how to configure static routes in a Kestrun server.
    FileName: 8-Static-OverrideRoutes.ps1
#>

# Import the Kestrun module
Install-PSResource -Name Kestrun

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Add a static files service
Add-KrStaticFilesService -RequestPath '/assets' -RootPath '.\Assets\wwwroot'

# Add a static map override
Add-KrStaticMapOverride -Path '/assets/override/pwsh' -ScriptBlock {

    $data = @{
        status  = 'ok'
        message = 'Static override works!'
    }

    Write-KrJsonResponse -InputObject $data
}

# Enable Kestrun configuration
Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
