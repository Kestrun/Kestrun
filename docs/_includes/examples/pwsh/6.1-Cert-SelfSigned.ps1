<#
    Create a self-signed development certificate and start HTTPS listener.
    FileName: 6.1-Cert-SelfSigned.ps1
#>

Initialize-KrRoot -Path $PSScriptRoot

# Configure default logging
New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -SetAsDefault

# Create a self-signed cert for localhost (RSA 2048 by default)
$cert = New-KrSelfSignedCertificate -DnsNames localhost, 127.0.0.1 -Exportable -ValidDays 30

# Show EKUs
Get-KrCertificatePurpose -Certificate $cert | Out-Host

# Configure HTTPS listener with the certificate
New-KrServer -Name "HTTPS Demo"
Add-KrEndpoint -Port 5001 -IPAddress ([IPAddress]::Loopback) -X509Certificate $cert -Protocols Http1

# Minimal route to verify HTTPS works
Add-KrPowerShellRuntime
Enable-KrConfiguration
Add-KrMapRoute -Verbs Get -Pattern "/hello" -ScriptBlock { Write-KrTextResponse "hello https" }

Start-KrServer

# Clean up and close all the loggers when the server stops
Close-KrLogger
