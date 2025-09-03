
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param()
<#
.SYNOPSIS
    Kestrun PowerShell Example: Multi Routes
.DESCRIPTION
    This script demonstrates how to define multiple routes in Kestrun, a PowerShell web server framework.
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
    # Import the Kestrun module from the source path if it exists, otherwise from installed modules
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    Write-Error "Failed to import Kestrun module: $_"
    Write-Error 'Ensure the Kestrun module is installed or the path is correct.'
    exit 1
}
New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\MultiRoutes.log' -RollingInterval Hour |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

$server = New-KrServer -Name 'Kestrun MultiRoutes'

if (Test-Path "$ScriptPath\devcert.pfx" ) {
    $cert = Import-KsCertificate -FilePath '.\devcert.pfx' -Password (ConvertTo-SecureString -String 'p@ss' -AsPlainText -Force)
} else {
    $cert = New-KrSelfSignedCertificate -DnsName 'localhost' -Exportable
    Export-KrCertificate -Certificate $cert `
        -FilePath "$ScriptPath\devcert" -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}

if (-not (Test-KsCertificate -Certificate $cert )) {
    Write-Error 'Certificate validation failed. Ensure the certificate is valid and not self-signed.'
    exit 1
}

$data = @'
[
  {
    "OrderId": 1001,
    "Customer": "Alice Johnson",
    "Product": "Kestrel Hoodie",
    "Quantity": 2,
    "UnitPrice": 39.95,
    "OrderTimestamp": "2025-07-29T13:42:00Z",
    "Shipped": true
  },
  {
    "OrderId": 1002,
    "Customer": "Boris Chen",
    "Product": "Powershell Mug",
    "Quantity": 1,
    "UnitPrice": 14.50,
    "OrderTimestamp": "2025-07-29T13:53:00Z",
    "Shipped": true
  },
  {
    "OrderId": 1003,
    "Customer": "Catalina Gómez",
    "Product": "Async Await Sticker",
    "Quantity": 10,
    "UnitPrice": 1.25,
    "OrderTimestamp": "2025-07-29T14:04:00Z",
    "Shipped": false
  },
  {
    "OrderId": 1004,
    "Customer": "Dmitri Novak",
    "Product": "CsvHelper Guidebook",
    "Quantity": 3,
    "UnitPrice": 24.00,
    "OrderTimestamp": "2025-07-29T14:12:00Z",
    "Shipped": false
  },
  {
    "OrderId": 1005,
    "Customer": "Emily O’Connor",
    "Product": "Kestrun Laptop Skin",
    "Quantity": 1,
    "UnitPrice": 17.75,
    "OrderTimestamp": "2025-07-29T14:18:00Z",
    "Shipped": true
  }
]
'@| ConvertFrom-Json

Set-KrSharedState -Name 'Orders' -Value $data
# Example usage:
Set-KrServerOption -AllowSynchronousIO -DenyServerHeader

Set-KrServerLimit -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
# Configure the listener (adjust port, cert path, and password)
Add-KrListener -Port 5001 -IPAddress ([IPAddress]::Loopback) -X509Certificate $cert -Protocols Http1AndHttp2AndHttp3
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

Add-KrResponseCompression -EnableForHttps -MimeTypes @('text/plain', 'text/html', 'application/json', 'application/xml', 'application/x-www-form-urlencoded')
Add-KrPowerShellRuntime



Add-KrBasicAuthentication -Name 'BasicAuth' -ScriptBlock {
    param($username, $password)
    Write-KrInformationLog -Message 'Basic Authentication: User {0} is trying to authenticate.' -Values $username
    if ($username -eq 'admin' -and $password -eq 'password') {
        $true
    } else {
        $false
    }
}

# Enable configuration
Enable-KrConfiguration

# Set-KrPythonRuntime

# Add a route with a script block
Add-KrMapRoute -Verbs Get -Pattern '/ps/json' -AuthorizationSchema 'BasicAuth' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Json Response'
    # Payload
    Write-KrJsonResponse -InputObject $Orders -StatusCode 200
}


Add-KrMapRoute -Verbs Get -Pattern '/ps/bson' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Bson Response'
    # Payload
    $payload = @{
        Body = 'Hello from PowerShell script! - Bson Response'
        RequestQuery = $Context.Request.Query
        RequestHeaders = $Context.Request.Headers
        RequestMethod = $Context.Request.Method
        RequestPath = $Context.Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody = $Context.Request.Body
    }
    Write-KrBsonResponse -InputObject $payload -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Pattern '/ps/cbor' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Cbor Response'
    # Payload
    $payload = @{
        Body = 'Hello from PowerShell script! - Cbor Response'
        RequestQuery = $Context.Request.Query
        RequestHeaders = $Context.Request.Headers
        RequestMethod = $Context.Request.Method
        RequestPath = $Context.Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody = $Context.Request.Body
    }
    Write-KrCborResponse -InputObject $payload -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Pattern '/ps/csv' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Csv Response'

    Write-KrCsvResponse -InputObject $Orders -StatusCode 200
}


Add-KrMapRoute -Verbs Get -Pattern '/ps/yaml' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Yaml Response'
    # Payload
    $payload = @{
        Body = 'Hello from PowerShell script! - Yaml Response'
        RequestQuery = $Context.Request.Query
        RequestHeaders = $Context.Request.Headers
        RequestMethod = $Context.Request.Method
        RequestPath = $Context.Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody = $Context.Request.Body
    }
    Write-KrYamlResponse -InputObject $payload -StatusCode 200
}


Add-KrMapRoute -Verbs Get -Pattern '/ps/xml' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Xml Response'
    # Payload
    $payload = @{
        Body = 'Hello from PowerShell script! - Xml Response'
        RequestQuery = $Context.Request.Query
        RequestHeaders = $Context.Request.Headers
        RequestMethod = $Context.Request.Method
        RequestPath = $Context.Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody = $Context.Request.Body
    }
    Write-KrXmlResponse -InputObject $payload -StatusCode 200
}


Add-KrMapRoute -Verbs Get -Pattern '/ps/text' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - Text Response'
    # Payload
    $payload = @{
        Body = 'Hello from PowerShell script! - Text Response'
        RequestQuery = $Context.Request.RequestQuery
        RequestHeaders = $Context.Request.Headers
        RequestMethod = $Context.Request.Method
        RequestPath = $Context.Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody = $Context.Request.Body
    } | Format-Table | Out-String
    Write-KrTextResponse -InputObject $payload -StatusCode 200
}


Add-KrMapRoute -Verbs Get -Pattern '/ps/file' -ScriptBlock {

    Write-Output 'Hello from PowerShell script! - file Response'
    Write-KrFileResponse -FilePath '..\..\README.md' -FileDownloadName 'README.md' -ContentDisposition Inline -StatusCode 200 -ContentType 'text/markdown'
}

Add-KrMapRoute -Verbs Get -Pattern '/cs/xml' -Language CSharp -Code @'

            Console.WriteLine("Hello from C# script! - Xml Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Xml Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteXmlResponse( payload,  200);
'@

Add-KrMapRoute -Verbs Get -Pattern '/cs/json' -Language CSharp -Code @'

            Console.WriteLine("Hello from C# script! - Json Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Json Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteJsonResponse( Orders,  200);
'@

Add-KrMapRoute -Verbs Get -Pattern '/cs/bson' -Language CSharp -Code @'

            Console.WriteLine("Hello from C# script! - Bson Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Bson Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteBsonResponse( payload,  200);
'@


Add-KrMapRoute -Verbs Get -Pattern '/cs/csv' -Language CSharp -Code @'

            Console.WriteLine("Hello from C# script! - Csv Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Csv Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteCsvResponse(Orders);
'@

Add-KrMapRoute -Verbs Get -Pattern '/cs/cbor' -Language CSharp -Code @'

            Console.WriteLine("Hello from C# script! - Cbor Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Cbor Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteCborResponse( payload,  200);
'@

Add-KrMapRoute -Verbs Get -Pattern '/cs/yaml' -Language CSharp -Code @'

            Console.WriteLine("Hello from C# script! - Yaml Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Yaml Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteYamlResponse( payload,  200);
'@

Add-KrMapRoute -Verbs Get -Pattern '/cs/text' -Language CSharp -Code @'

            Console.WriteLine("Hello from C# script! - Text Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Text Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteTextResponse( payload,  200);
'@


Add-KrMapRoute -Verbs Get -Pattern '/cs/file' -Language CSharp -Code @'

    Console.WriteLine("Hello from C# script! - file Response(From C#)");
    Context.Response.WriteFileResponse("..\\..\\README.md", null, 200);
'@

Add-KrMapRoute -Verbs Get -Pattern '/messagestream' -ScriptBlock {
    $DebugPreference = 'Continue'
    $VerbosePreference = 'Continue'

    Write-Output 'Hello from PowerShell script!'
    Write-Warning 'This is a warning message.'
    Write-Verbose 'This is a verbose message.'
    Write-Error 'This is an error message.'
    Write-Host 'This is a host message.'
    Write-Information 'This is an information message.'
    Write-Debug 'This is a debug message.'
}



# ------------------------------------------------------------------
# 1. PowerShell route  ─ /hello-ps
# ------------------------------------------------------------------
Add-KrMapRoute -Verbs Get -Pattern '/hello-ps' -ScriptBlock {
    $Context.Response.ContentType = 'text/plain'
    $Context.Response.Body = "Hello from PowerShell at $(Get-Date -Format o)"
}

# ------------------------------------------------------------------
# 2. C# script route  ─ /hello-cs
#    (wrap the C# source in a here-string *inside* the ScriptBlock)
# ------------------------------------------------------------------
Add-KrMapRoute -Verbs Get -Pattern '/hello-cs' -Language CSharp -Code @'

Context.Response.ContentType = "text/plain";
Context.Response.Body        = $"Hello from C# at {DateTime.UtcNow:o}";
'@




Add-KrMapRoute -Verbs Get -Pattern '/status' -ScriptBlock {
    $html = @'
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Server Status</title>
</head>
<body>
  <h1>Status for {{ApplicationName}}</h1>
  <ul>
    <li>Path: {{Request.Path}}</li>
    <li>Method: {{Request.Method}}</li>
    <li>Time: {{Timestamp}}</li>
    <li>Hits: {{Visits}}</li>
  </ul>
</body>
</html>
'@

    Expand-KrObject -InputObject $Context -Label 'Context'

    Expand-KrObject -InputObject $Context.Request -Label 'Request'

    Write-KrHtmlResponse -Template $html -Variables @{
        ApplicationName = $server.ApplicationName
        Request = $Context.Request
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        Visits = Get-Random
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/vb/text' -Language VBNet -Code @'
    Console.WriteLine("Hello from VB.NET script! - Text Response (From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Text Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }

    Await Context.Response.WriteTextResponseAsync(payload, 200)
'@


Add-KrMapRoute -Verbs Get -Pattern '/vb/xml' -Language VBNet -Code @'
    Console.WriteLine("Hello from VB.NET script! - Xml Response(From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Xml Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }

    Await Context.Response.WriteXmlResponseAsync(payload, 200)
'@

Add-KrMapRoute -Verbs Get -Pattern '/vb/yaml' -Language VBNet -Code @'
    Console.WriteLine("Hello from VB.NET script! - Yaml Response(From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Yaml Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }

    Await Context.Response.WriteYamlResponseAsync(payload, 200)
'@


Add-KrMapRoute -Verbs Get -Pattern '/vb/json' -Language VBNet -Code @'
    Console.WriteLine("Hello from VB.NET script! - Json Response(From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Json Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }

    Await Context.Response.WriteJsonResponseAsync(payload, 200)
'@



# Start the server asynchronously
Start-KrServer

