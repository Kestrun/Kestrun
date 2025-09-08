<#
    Sample Kestrun Server - HTTPS
    Demonstrates adding an HTTPS listener with a development self-signed certificate.
    For production use a proper certificate and secure secret storage.
    FileName: 7.3-Https.ps1
#>

# (Optional) Configure console logging
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Ensure a development certificate exists (self-signed) - dev ONLY
$certPath = Join-Path $PSScriptRoot 'devcert.pfx'
if (-not (Test-Path $certPath)) {
    Write-Host 'Creating development self-signed certificate devcert.pfx'
    $pw = Read-Host -Prompt 'Enter temporary password for devcert.pfx (dev only)' -AsSecureString
    New-KrSelfSignedCertificate -DnsName 'localhost' -Path $certPath -Password $pw | Out-Null
} else {
    $pw = Read-Host -Prompt 'Enter password for existing devcert.pfx' -AsSecureString
}

# Create a new Kestrun server
New-KrServer -Name 'Endpoints Https'

# HTTP listener (optional)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# HTTPS listener on port 5443
Add-KrListener -Port 5443 -IPAddress ([IPAddress]::Loopback) -CertPath $certPath -CertPassword $pw

# Add PowerShell runtime
Add-KrPowerShellRuntime

# Enable configuration
Enable-KrConfiguration

# Secure route
Add-KrMapRoute -Verbs Get -Pattern '/secure' -ScriptBlock {
    Write-KrLog -Level Information -Message 'Secure endpoint invoked'
    Write-KrTextResponse -InputObject 'Secure hello' -StatusCode 200
}

Write-KrLog -Level Information -Message 'HTTPS listener active on 5443'

# Start the server
Start-KrServer
