<#
    Create a self-signed development certificate and start HTTPS listener.
    FileName: 10.3-Https-Redirection.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

# Configure default logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -SetAsDefault

# Create a self-signed cert for localhost (RSA 2048 by default)
$cert = New-KrSelfSignedCertificate -DnsNames localhost, 127.0.0.1 -Exportable -ValidDays 30


# Configure HTTPS listener with the certificate
New-KrServer -Name "HTTPS Redirection Demo"
Add-KrEndpoint -Port ($Port) -IPAddress $IPAddress
Add-KrEndpoint -Port ($Port + 443) -IPAddress $IPAddress -X509Certificate $cert

Add-KrHttpsRedirection -RedirectStatusCode 301 -HttpsPort ($Port + 443)

Add-KrPowerShellRuntime

Enable-KrConfiguration

# Minimal route to verify HTTPS redirect works
Add-KrMapRoute -Verbs Get -Pattern "/" -ScriptBlock { Write-KrTextResponse "hello https" }

Start-KrServer

# Clean up and close all the loggers when the server stops
Close-KrLogger
