<#
    Sample Kestrun Server - HTTPS
    Demonstrates adding an HTTPS listener with a development self-signed certificate.
    For production use a proper certificate and secure secret storage.
    FileName: 7.3-Https.ps1
#>

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    [int]$HttpPort = 5000,
    [int]$HttpsPort = 5443,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# (Optional) Configure console logging
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Ensure a development certificate exists (self-signed) - dev ONLY
$certPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'devcert.pfx'
$pw = ConvertTo-SecureString -String 'P@ssw0rd!' -AsPlainText -Force
if (-not (Test-Path $certPath)) {
    Write-Host 'Creating development self-signed certificate devcert.pfx'
    New-KrSelfSignedCertificate -DnsName 'localhost' -Exportable -ValidDays 30 |
        Export-KrCertificate -FilePath $certPath -Format Pfx -IncludePrivateKey -Password $pw | Out-Null
}

# Create a new Kestrun server
New-KrServer -Name 'Endpoints Https'

# HTTP listener (optional)
Add-KrListener -Port $HttpPort -IPAddress $IPAddress

# HTTPS listener
Add-KrListener -Port $HttpsPort -IPAddress $IPAddress -CertPath $certPath -CertPassword $pw

# Add PowerShell runtime
Add-KrPowerShellRuntime

# Enable configuration
Enable-KrConfiguration

# Secure route
Add-KrMapRoute -Verbs Get -Pattern '/secure' -ScriptBlock {
    Write-KrLog -Level Information -Message 'Secure endpoint invoked'
    Write-KrTextResponse -InputObject 'Secure hello' -StatusCode 200
}

Write-KrLog -Level Information -Message 'HTTPS listener active on {HttpsPort}' -Properties $HttpsPort


# Start the server
Start-KrServer -CloseLogsOnExit
