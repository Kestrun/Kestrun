<#
    Sample Kestrun Server Configuration with Multiple Languages
    This script demonstrates how to set up a simple Kestrun server with multiple routes and multiple languages.
    Kestrun supports PowerShell, CSharp, and VBNet.
    FileName: 2.2-Multi-Language-Routes.ps1
#>

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route with PowerShell
Add-KrMapRoute -Verbs Get -Path "/hello-powershell" -ScriptBlock {
    Write-KrTextResponse -InputObject "Hello, World!" -StatusCode 200
}

# Map the route with CSharp
Add-KrMapRoute -Verbs Get -Path "/hello-csharp" -Code @"
    await Context.Response.WriteTextResponseAsync(inputObject: "Hello, World!", statusCode: 200);
    // Or the synchronous version
    // Context.Response.WriteTextResponse( "Hello, World!", 200);
"@ -Language CSharp

# Map the route with VBNet
Add-KrMapRoute -Verbs Get -Path "/hello-vbnet" -Code @"
    Await Context.Response.WriteTextResponseAsync( "Hello, World!", 200)
    ' Or the synchronous version
    ' Context.Response.WriteTextResponse( "Hello, World!", 200);
"@ -Language VBNet

# Start the server asynchronously
Start-KrServer
