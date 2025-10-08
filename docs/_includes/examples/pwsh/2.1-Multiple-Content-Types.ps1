<#
    Sample Kestrun Server Configuration with Multiple Content Types
    This script demonstrates how to set up a simple Kestrun server with multiple routes.
    The server will respond with different content types based on the requested route.
    FileName: 2.1-Multiple-Content-Types.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Create a new Kestrun server
New-KrServer -Name 'Simple Server'

# Add a listener on the configured port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress



# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route
Add-KrMapRoute -Verbs Get -Path '/hello' -ScriptBlock {
    Write-KrTextResponse -InputObject 'Hello, World!' -StatusCode 200
}

# Map the route for JSON response
Add-KrMapRoute -Verbs Get -Path '/hello-json' -ScriptBlock {
    Write-KrJsonResponse -InputObject @{ message = 'Hello, World!' } -StatusCode 200
}

# Map the route for XML response
Add-KrMapRoute -Verbs Get -Path '/hello-xml' -ScriptBlock {
    Write-KrXmlResponse -InputObject @{ message = 'Hello, World!' } -StatusCode 200
}

# Map the route for YAML response
Add-KrMapRoute -Verbs Get -Path '/hello-yaml' -ScriptBlock {
    Write-KrYamlResponse -InputObject @{ message = 'Hello, World!' } -StatusCode 200
}


# Start the server asynchronously
Start-KrServer
