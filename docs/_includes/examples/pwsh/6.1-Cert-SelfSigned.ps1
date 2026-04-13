<#
    Create a development root CA, issue a localhost leaf certificate, optionally trust the root,
    and start an HTTPS listener.
    FileName: 6.1-Cert-SelfSigned.ps1
#>
param(
    [int]$Port = $env:PORT ?? 5000,

    [switch]$TrustRoot
)

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

# Configure default logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -SetAsDefault

$bundle = New-KrDevelopmentCertificate `
    -DnsNames 'localhost', '127.0.0.1', '::1' `
    -Exportable `
    -LeafValidDays 30 `
    -RootValidDays 3650 `
    -TrustRoot:$TrustRoot.IsPresent

$root = $bundle.RootCertificate
$cert = $bundle.LeafCertificate

Write-Host "Development root: $($root.Subject)" -ForegroundColor Cyan
Write-Host "Leaf certificate: $($cert.Subject)" -ForegroundColor Cyan

if ($bundle.RootTrusted) {
    Write-Host 'Trusted the development root in CurrentUser\\Root.' -ForegroundColor Green
} elseif ($TrustRoot.IsPresent) {
    Write-Host 'TrustRoot was requested but no Windows trust action was performed.' -ForegroundColor Yellow
} else {
    Write-Host 'Root trust skipped. Use -TrustRoot on Windows if you want Chromium-family browsers to trust the root.' -ForegroundColor Yellow
}

# Show EKUs
Get-KrCertificatePurpose -Certificate $cert | Out-Host

# Configure HTTPS listener with the certificate
New-KrServer -Name 'HTTPS Demo'
Add-KrEndpoint -Port $Port -X509Certificate $cert -Protocols Http1

# Minimal route to verify HTTPS works

Enable-KrConfiguration
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock { Write-KrTextResponse 'hello https' }

Start-KrServer

# Clean up and close all the loggers when the server stops
Close-KrLogger
