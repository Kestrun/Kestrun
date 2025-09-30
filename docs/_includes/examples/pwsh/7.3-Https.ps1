<#
    Sample Kestrun Server - HTTPS
    Demonstrates adding an HTTPS listener with a development self-signed certificate.
    For production use a proper certificate and secure secret storage.
    FileName: 7.3-Https.ps1
#>

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    [int]$Port = 5000,
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

$port2 = $Port + 443
# Create a new Kestrun server
New-KrServer -Name 'Endpoints Https'

# HTTP listener (optional)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# HTTPS listener
Add-KrEndpoint -Port $port2 -IPAddress $IPAddress -CertPath $certPath -CertPassword $pw

# Add PowerShell runtime
Add-KrPowerShellRuntime

# Enable configuration
Enable-KrConfiguration

# Secure route
Add-KrMapRoute -Verbs Get -Pattern '/secure' -Endpoints "$($IPAddress.ToString()):$port2" -ScriptBlock {
    Write-KrLog -Level Information -Message 'Secure endpoint invoked'
    Write-KrTextResponse -InputObject 'Secure hello' -StatusCode 200
}

# Unsecure route (available on HTTP listener)
Add-KrMapRoute -Verbs Get -Pattern '/unsecure' -Endpoints "$($IPAddress.ToString()):$Port" -ScriptBlock {
    Write-KrLog -Level Information -Message 'Unsecure endpoint invoked'
    Write-KrTextResponse -InputObject 'Unsecure hello' -StatusCode 200
}

Write-KrLog -Level Information -Message 'HTTP listener active on {Port}' -Values $Port
Write-KrLog -Level Information -Message 'HTTPS listener active on {Port2}' -Values $port2

# Start the server
Start-KrServer -CloseLogsOnExit
