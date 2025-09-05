<#
    Sample Kestrun Server on how to use route parameters.
    These examples demonstrate how to access route parameters and query strings in a Kestrun server.
    FileName: 2.3-Route-Parameters.ps1
#>

New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Enable Kestrun configuration
Enable-KrConfiguration

# Route group with a common prefix
Add-KrRouteGroup -Prefix "/input" -ScriptBlock {

    # Path parameter example
    Add-KrMapRoute -Verbs Get -Pattern "/{value}" -ScriptBlock {
        $value = Get-KrRequestRouteParam -Name 'value'
        Write-KrTextResponse -InputObject "The Path Parameter 'value' was: $value" -StatusCode 200
    }

    # Query string example
    Add-KrMapRoute -Verbs Patch -ScriptBlock {
        $value = Get-KrRequestQuery -Name 'value'
        Write-KrTextResponse -InputObject "The Query String 'value' was: $value" -StatusCode 200
    }

    # Body parameter example
    Add-KrMapRoute -Verbs Post -ScriptBlock {
        $body = Get-KrRequestBody
        Write-KrTextResponse -InputObject "The Body Parameter 'value' was: $($body.value)" -StatusCode 200
    }

    # Header parameter example
    Add-KrMapRoute -Verbs Put -ScriptBlock {
        $value = Get-KrRequestHeader -Name 'value'
        Write-KrTextResponse -InputObject "The Header Parameter 'value' was: $value" -StatusCode 200
    }

    # Cookie parameter example
    Add-KrMapRoute -Verbs Delete -ScriptBlock {
        $value = Get-KrRequestCookie -Name 'value'
        Write-KrTextResponse -InputObject "The Cookie Parameter 'value' was: $value" -StatusCode 200
    }
}

# Start the server asynchronously
Start-KrServer
