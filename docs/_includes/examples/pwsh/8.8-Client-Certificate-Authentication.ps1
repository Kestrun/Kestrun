<#
    Sample: Client Certificate Authentication
    Purpose: Demonstrates client certificate (mTLS) authentication with X.509 certificate validation.
    File:    8.8-Client-Certificate-Authentication.ps1
    Notes:   Requires creating test certificates. See documentation for certificate setup.
#>
param(
    [int]$Port = 5001,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# 1. (Optional) Logging pipeline
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server host
New-KrServer -Name 'Client Certificate Auth Demo'

# 3. Create or load server certificate for HTTPS
$serverCertPath = Join-Path $PSScriptRoot 'server-cert.pfx'
if (Test-Path $serverCertPath) {
    $serverCert = Import-KrCertificate -FilePath $serverCertPath -Password (ConvertTo-SecureString -String 'test' -AsPlainText -Force)
} else {
    # Create a self-signed CA for testing
    $ca = New-KrSelfSignedCertificate -DnsNames 'Test CA' -CertificateAuthority -Exportable
    
    # Create server certificate signed by CA
    $serverCert = New-KrSelfSignedCertificate -DnsNames 'localhost' -SigningCertificate $ca -Exportable
    
    # Export for reuse
    Export-KrCertificate -Certificate $serverCert -FilePath $serverCertPath `
        -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String 'test' -AsPlainText -Force)
    
    # Also create and export a client certificate for testing
    $clientCert = New-KrSelfSignedCertificate -DnsNames 'test-client' -SigningCertificate $ca -ClientAuth -Exportable
    Export-KrCertificate -Certificate $clientCert -FilePath (Join-Path $PSScriptRoot 'client-cert.pfx') `
        -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String 'test' -AsPlainText -Force)
    
    Write-Host "Created test certificates:"
    Write-Host "  Server: $serverCertPath"
    Write-Host "  Client: $(Join-Path $PSScriptRoot 'client-cert.pfx')"
    Write-Host "  Password: test"
}

# 4. Add HTTPS endpoint with client certificate requirement
Add-KrEndpoint -Https -Port $Port -IPAddress $IPAddress -Certificate $serverCert -RequireClientCertificate

# 5. Configure client certificate authentication with validation
Add-KrClientCertificateAuthentication -AuthenticationScheme 'Certificate' `
    -ValidateCertificateUse `
    -ValidateValidityPeriod

# 6. Finalize configuration (build internal pipeline)
Enable-KrConfiguration

# 7. Map secured route group using certificate authentication
Add-KrRouteGroup -Prefix '/secure/cert' -AuthorizationScheme 'Certificate' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $cert = $Context.Connection.ClientCertificate
        Write-KrJsonResponse @{
            message = "Hello from client certificate authentication"
            subject = $cert.Subject
            issuer = $cert.Issuer
            thumbprint = $cert.Thumbprint
            validFrom = $cert.NotBefore.ToString('o')
            validTo = $cert.NotAfter.ToString('o')
        }
    }
    
    Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock {
        $cert = $Context.Connection.ClientCertificate
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
}

# 8. Add a public info endpoint
Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock {
    Write-KrJsonResponse @{
        message = "Client Certificate Authentication Demo"
        endpoints = @(
            @{
                path = "/secure/cert/hello"
                method = "GET"
                description = "Returns certificate information"
                requiresAuth = $true
            }
            @{
                path = "/secure/cert/info"
                method = "GET"
                description = "Returns detailed authentication and claims information"
                requiresAuth = $true
            }
        )
        testInstructions = @{
            clientCertPath = "client-cert.pfx (in same directory as script)"
            password = "test"
            example = "Invoke-RestMethod -Uri 'https://localhost:$Port/secure/cert/hello' -Certificate `$cert -SkipCertificateCheck"
        }
    }
}

# 9. Start server (Ctrl+C to stop)
Write-Host "`nServer starting on https://localhost:$Port"
Write-Host "Test the secured endpoint with:"
Write-Host "  `$cert = Import-PfxCertificate -FilePath 'client-cert.pfx' -Password (ConvertTo-SecureString 'test' -AsPlainText -Force) -CertStoreLocation 'Cert:\CurrentUser\My'"
Write-Host "  Invoke-RestMethod -Uri 'https://localhost:$Port/secure/cert/hello' -Certificate `$cert -SkipCertificateCheck"
Write-Host "`nPress Ctrl+C to stop"

Start-KrServer -CloseLogsOnExit
