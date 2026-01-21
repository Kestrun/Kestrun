<#
    Sample: Client Certificate Authentication
    Purpose: Demonstrates client certificate (mTLS) authentication with X.509 certificate validation.
    File:    8.8-Client-Certificate-Authentication.ps1
    Notes:   Uses self-signed certificates for testing. For production, use CA-signed certificates.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# 1. (Optional) Logging pipeline
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server host
New-KrServer -Name 'Client Certificate Auth Demo'

# 3. Create or load server certificate for HTTPS
$serverCertPath = Join-Path $PSScriptRoot 'server-cert.pfx'
if (Test-Path $serverCertPath) {
    $serverCert = Import-KrCertificate -FilePath $serverCertPath -Password (ConvertTo-SecureString -String 'test' -AsPlainText -Force)
} else {
    # Create server certificate for localhost
    $serverCert = New-KrSelfSignedCertificate -DnsNames 'localhost' -Exportable

    # Export for reuse
    Export-KrCertificate -Certificate $serverCert -FilePath $serverCertPath `
        -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String 'test' -AsPlainText -Force)

    Write-Host "Created server certificate: $serverCertPath"
}

# 4. Create or load client certificate for testing
$clientCertPath = Join-Path $PSScriptRoot 'client-cert.pfx'
if (-not (Test-Path $clientCertPath)) {
    # Create a self-signed client certificate for testing
    # Note: In production, client certificates should be issued by a trusted CA
    $clientCert = New-KrSelfSignedCertificate -DnsNames 'test-client' -Exportable

    # Export for testing
    Export-KrCertificate -Certificate $clientCert -FilePath $clientCertPath `
        -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String 'test' -AsPlainText -Force)

    Write-Host 'Created test certificates:'
    Write-Host "  Server: $serverCertPath"
    Write-Host "  Client: $clientCertPath"
    Write-Host '  Password: test'
    Write-Host ''
    Write-Host 'Note: Using self-signed certificates for testing.'
    Write-Host '      In production, use certificates from a trusted CA.'
    Write-Host ''
}

# 5. Configure HTTPS to require client certificates
# NOTE: This callback is invoked on Kestrel threadpool threads during TLS handshake.
# It must be a pure .NET delegate (not a PowerShell scriptblock), otherwise you'll
# get "There is no Runspace available" errors.
$clientCertValidation = [System.Func[
    System.Security.Cryptography.X509Certificates.X509Certificate2,
    System.Security.Cryptography.X509Certificates.X509Chain,
    System.Net.Security.SslPolicyErrors,
    bool
]] [Kestrun.Certificates.ClientCertificateValidationCallbacks]::AllowMissingOrSelfSignedForDevelopment

Set-KrServerHttpsOptions -ClientCertificateMode AllowCertificate -ClientCertificateValidation $clientCertValidation

# 6. Add HTTPS endpoint
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $serverCert

# 7. Configure client certificate authentication
# Note: For self-signed client certificates, we need to allow all certificate types
# and disable some validation that requires a proper certificate chain
Add-KrClientCertificateAuthentication -AuthenticationScheme 'Certificate' -AllowedCertificateTypes All -RevocationMode NoCheck

# 8. Finalize configuration (build internal pipeline)
Enable-KrConfiguration

# 9. Map secured route group using certificate authentication
Add-KrMapRoute -Verbs Get -Pattern '/secure/cert/hello' -AuthorizationScheme 'Certificate' -ScriptBlock {
    $cert = $Context.Connection.ClientCertificate
    if ($null -eq $cert) {
        Write-KrJsonResponse @{
            error = 'No client certificate provided'
        } -StatusCode 401
        return
    }

    Write-KrJsonResponse @{
        message = 'Hello from client certificate authentication'
        subject = $cert.Subject
        issuer = $cert.Issuer
        thumbprint = $cert.Thumbprint
        validFrom = $cert.NotBefore.ToString('o')
        validTo = $cert.NotAfter.ToString('o')
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/secure/cert/info' -AuthorizationScheme 'Certificate' -ScriptBlock {
    $cert = $Context.Connection.ClientCertificate
    if ($null -eq $cert) {
        Write-KrJsonResponse @{
            error = 'No client certificate provided'
        } -StatusCode 401
        return
    }
    $claims = $Context.User.Claims | ForEach-Object {
        @{
            type = $_.Type
            value = $_.Value
        }
    }
    Write-KrJsonResponse @{
        authenticationType = $Context.User.Identity.AuthenticationType
        isAuthenticated = $Context.User.Identity.IsAuthenticated
        name = $Context.User.Identity.Name
        claims = $claims
        certificate = @{
            subject = $cert.Subject
            issuer = $cert.Issuer
            thumbprint = $cert.Thumbprint
            serialNumber = $cert.SerialNumber
        }
    }
}

# 10. Add a public info endpoint
Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock {
    Write-KrJsonResponse @{
        message = 'Client Certificate Authentication Demo'
        endpoints = @(
            @{
                path = '/secure/cert/hello'
                method = 'GET'
                description = 'Returns certificate information'
                requiresAuth = $true
            }
            @{
                path = '/secure/cert/info'
                method = 'GET'
                description = 'Returns detailed authentication and claims information'
                requiresAuth = $true
            }
        )
        testInstructions = @{
            clientCertPath = 'client-cert.pfx (in same directory as script)'
            password = 'test'
            example = "Invoke-RestMethod -Uri 'https://localhost:$Port/secure/cert/hello' -Certificate `$cert -SkipCertificateCheck"
        }
    }
}

# 11. Start server (Ctrl+C to stop)
Write-Host "`nServer starting on https://localhost:$Port"
Write-Host 'Test the secured endpoint with:'
Write-Host "  `$cert = Import-KrCertificate -FilePath '$clientCertPath' -Password (ConvertTo-SecureString 'test' -AsPlainText -Force)"
Write-Host "  Invoke-RestMethod -Uri 'https://localhost:$Port/secure/cert/hello' -Certificate `$cert -SkipCertificateCheck"
Write-Host "`nPress Ctrl+C to stop"

Start-KrServer -CloseLogsOnExit
