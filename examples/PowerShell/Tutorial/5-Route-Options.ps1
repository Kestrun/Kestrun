<#
    Sample Kestrun Server Configuration using MapRouteOptions class.
    These examples demonstrate how to use the MapRouteOptions class to configure routes in a Kestrun server.
    FileName: 5-Route-Options.ps1
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

# Map the route
Add-KrMapRoute -Verbs Get -Pattern "/xml/{message}" -ScriptBlock {
    $message = Get-KrRequestRouteParam -Name 'message'
    Write-KrXmlResponse -InputObject @{ message = $message } -StatusCode 200
}

# YAML Route using MapRouteOption
Add-KrMapRoute -Options (New-MapRouteOption -Property @{
        Pattern = "/yaml"
        HttpVerbs = 'Get'
        Code = {
            $message = Get-KrRequestRouteParam -Name 'message'
            Write-KrYamlResponse -InputObject @{ message = $message } -StatusCode 200
        }
        Language = 'PowerShell'
        DisableAntiforgery = $true
    }
)

# JSON Route using MapRouteOption
$options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
$options.Pattern = "/json"
$options.HttpVerbs = [Kestrun.Utilities.HttpVerb[]] @('get')
$options.Code = {
    $message = Get-KrRequestHeader -Name 'message'
    Write-KrJsonResponse -InputObject @{ message = $message } -StatusCode 200
}
$options.Language = 'PowerShell'
Add-KrMapRoute -Options $options

# Text Route using MapRouteOption and Pipeline
New-MapRouteOption -Property @{
    Pattern = "/txt"
    HttpVerbs = 'Get'
    Code = @"
            var message= Context.Request.Query["message"];
            Context.Response.WriteTextResponse($"message = {message}");
"@
    Language = 'CSharp'
} | Add-KrMapRoute

# Start the server asynchronously
Start-KrServer
