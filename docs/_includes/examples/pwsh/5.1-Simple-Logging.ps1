<#
    Sample Kestrun Server demonstrating basic logging.
    Creates a logger with file + console sinks, registers it by name, then maps simple routes.
    Use -SetAsDefault on Register-KrLogger or pass -Logger/-LoggerName to New-KrServer to enable framework logs.
    FileName: 5.1-Simple-Logging.ps1
#>

$myLogger = New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\sample.log' -RollingInterval Hour |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -PassThru

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

Write-KrLog -Level Information -Logger $myLogger -Message "Kestrun server created and listener added."
# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route with PowerShell
Add-KrMapRoute -Verbs Get -Path "/hello-powershell" -ScriptBlock {
    $response = "Hello, World!"
    Write-KrLog -Logger $myLogger -Level Debug -Message "Handling /hello-powershell response {response}" -Properties $response
    Write-KrTextResponse -InputObject $response -StatusCode 200
}

# Map the route with CSharp
Add-KrMapRoute -Verbs Get -Path "/hello-csharp" -Code @"
    var response = "Hello, World!";
    // Log the response using the provided logger instance 'myLogger' (shared from PowerShell)
    myLogger.Debug("Handling /hello-csharp response {response}", response);
    Context.Response.WriteTextResponse( response, 200);
"@ -Language CSharp

# Start the server asynchronously
Start-KrServer

# Clean up and close the logger when the server stops
Close-KrLogger -Logger $myLogger
