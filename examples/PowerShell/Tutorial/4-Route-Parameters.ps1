<#
    Sample Kestrun Server on how to use route parameters.
    These examples demonstrate how to access route parameters and query strings in a Kestrun server.
    FileName: 4-Route-Parameters.ps1
#>

# Import the Kestrun module
Install-PSResource -Name Kestrun

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Enable Kestrun configuration
Enable-KrConfiguration

# Path parameter example
Add-KrMapRoute -Verbs Get -Pattern "/input/{value}" -ScriptBlock {
    $value = Get-KrRequestRouteParam -Name 'value'
    Write-KrTextResponse -InputObject "The Path Parameter 'value' was: $value" -StatusCode 200
}

# Query string example
Add-KrMapRoute -Verbs Get -Pattern "/input" -ScriptBlock {
    $value = Get-KrRequestQuery -Name 'value'
    Write-KrTextResponse -InputObject "The Query String 'value' was: $value" -StatusCode 200
}

# Body parameter example
Add-KrMapRoute -Verbs Post -Pattern "/input" -ScriptBlock {
    $value = Get-KrRequestBody -Name 'value'
    Write-KrTextResponse -InputObject "The Body Parameter 'value' was: $value" -StatusCode 200
}

# Header parameter example
Add-KrMapRoute -Verbs Get -Pattern "/input" -ScriptBlock {
    $value = Get-KrRequestHeader -Name 'value'
    Write-KrTextResponse -InputObject "The Header Parameter 'value' was: $value" -StatusCode 200
}

# Cookie parameter example
Add-KrMapRoute -Verbs Get -Pattern "/input" -ScriptBlock {
    $value = Get-KrRequestCookie -Name 'value'
    Write-KrTextResponse -InputObject "The Cookie Parameter 'value' was: $value" -StatusCode 200
}

# Start the server asynchronously
Start-KrServer
