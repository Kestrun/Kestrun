#
# Sample: Status Code Pages with PowerShell Script
# This script demonstrates how to set up a Kestrun server with PowerShell script options.
# The server will use PowerShell scripts to generate custom error responses.
# FileName: 17.4-StatusCodePages-CustomCSharp.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback # Use 'Loopback' for safety in tests/examples
)

New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name 'Status Code Pages with Script Server'

# Add a listener on the specified port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Create C# script options for status code handling
$code = @'
    var __resp = Context.Response;
    var __req  = Context.Request;

    // Extract request info
    int    __sc    = __resp.StatusCode;
    string __path  = __req.Path.ToString();
    string __meth  = __req.Method;
    string __ua    = __req.Headers["User-Agent"].ToString();

    // Log to console (yellow)
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"StatusCode {__sc} for {__meth} {__path}");
    Console.ResetColor();

    // Build response object
    var __payload = new
    {
        error      = true,
        statusCode = __sc,
        path       = __path,
        method     = __meth,
        timestamp  = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
        userAgent  = __ua,
        description = __sc switch
        {
            404 => "Page not found - The requested resource does not exist",
            500 => "Internal server error - Something went wrong on our end",
            403 => "Forbidden - You don't have permission to access this resource",
            401 => "Unauthorized - Please authenticate to access this resource",
            _   => $"Error {__sc} occurred"
        },
        suggestion = __sc switch
        {
            404 => "Check the URL or try our home page",
            500 => "Please try again later or contact support",
            403 => "Contact an administrator for access",
            401 => "Please log in to continue",
            _   => "Please try again or contact support"
        }
    };

    // Write JSON response
    __resp.ContentType = "application/json; charset=utf-8";
    __resp.Headers["Cache-Control"] = "no-store, must-revalidate, no-cache, max-age=0";
    //__resp.ContentLength = null; // let the server compute if needed

    await __resp.WriteJsonResponseAsync(__payload,__resp.StatusCode);

'@

# Enable status code pages with PowerShell script
Enable-KrStatusCodePage -Code $code -Language CSharp

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a normal route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrJsonResponse -InputObject @{
        message = 'Hello, World!'
        timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        status = 'success'
    } -StatusCode 200
}

# Map routes that trigger different status codes
Add-KrMapRoute -Verbs Get -Pattern '/notfound' -ScriptBlock {
    # Return empty response with 404 status to trigger custom error page
    $Context.Response.StatusCode = 404
}

Add-KrMapRoute -Verbs Get -Pattern '/error' -ScriptBlock {
    # Return empty response with 500 status to trigger custom error page
    $Context.Response.StatusCode = 500
}

Add-KrMapRoute -Verbs Get -Pattern '/forbidden' -ScriptBlock {
    # Return empty response with 403 status to trigger custom error page
    $Context.Response.StatusCode = 403
}

Add-KrMapRoute -Verbs Get -Pattern '/unauthorized' -ScriptBlock {
    # Return empty response with 401 status to trigger custom error page
    $Context.Response.StatusCode = 401
}

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
