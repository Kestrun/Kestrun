<#
    Sample Kestrun Server Configuration using MapRouteOptions class.
    These examples demonstrate how to use the MapRouteOptions class to configure routes in a Kestrun server.
    FileName: 2.4-Route-Options.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Create a new Kestrun server
New-KrServer -Name 'Simple Server'

# Add a listener on the configured port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add the PowerShell runtime


# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route
Add-KrMapRoute -Verbs Get -Pattern '/xml/{message}' -ScriptBlock {
    $message = Get-KrRequestRouteParam -Name 'message'
    Write-KrXmlResponse -InputObject @{ message = $message } -StatusCode 200
}

# YAML Route using MapRouteOption
Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
        Pattern = '/yaml'
        HttpVerbs = 'Get'
        ScriptCode = @{
            Code = {
                $message = Get-KrRequestCookie -Name 'message'
                Write-KrYamlResponse -InputObject @{ message = $message } -StatusCode 200
            }
            Language = 'PowerShell'
        }
        DisableAntiforgery = $true
    }
)

# JSON Route using MapRouteOption
$options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
$options.Pattern = '/json'
$options.HttpVerbs = [Kestrun.Utilities.HttpVerb[]] @('get')
$options.ScriptCode = @{
    Code = {
        $message = Get-KrRequestHeader -Name 'message'
        Write-KrJsonResponse -InputObject @{ message = $message } -StatusCode 200
    }
    Language = 'PowerShell'
}
Add-KrMapRoute -Options $options

# Text Route using MapRouteOption and Pipeline
New-KrMapRouteOption -Property @{
    Pattern = '/txt'
    HttpVerbs = 'Get'
    ScriptCode = @{
        Code = @'
            var message= Context.Request.Query["message"];
            Context.Response.WriteTextResponse($"message = {message}");
'@
        Language = 'CSharp'
    }
} | Add-KrMapRoute


# Start the server asynchronously
Start-KrServer
