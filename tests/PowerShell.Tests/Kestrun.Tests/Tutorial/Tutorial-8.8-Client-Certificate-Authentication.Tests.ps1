[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.8 Client Certificate Authentication' -Tag 'Tutorial', 'Slow' {
    BeforeAll {

        # Start the tutorial script
        $script:instance = Start-ExampleScript -Name '8.8-Client-Certificate-Authentication.ps1'

        # Wait a moment for server to fully initialize
        Start-Sleep -Seconds 2

        # Import the client certificate that was created by the running script.
        # Start-ExampleScript executes a temp-copied script, so $PSCommandPath (and the basename used
        # for output files) is randomized. Locate the generated PFX instead of guessing its name.
        $runRoot = Split-Path -Parent $script:instance.TempPath
        $certDir = Join-Path -Path $runRoot -ChildPath 'certs'
        $runBaseName = [System.IO.Path]::GetFileNameWithoutExtension($script:instance.TempPath)
        $expectedClientPfxPath = Join-Path -Path $certDir -ChildPath "$runBaseName-client-cert.pfx"

        $clientPfx = if (Test-Path $expectedClientPfxPath) {
            Get-Item -Path $expectedClientPfxPath
        } else {
            Get-ChildItem -Path $certDir -Filter '*-client-cert.pfx' -ErrorAction SilentlyContinue |
                Sort-Object -Property LastWriteTime -Descending |
                Select-Object -First 1
        }

        if (-not $clientPfx) {
            throw "Client certificate PFX not found under: $certDir"
        }

        $password = ConvertTo-SecureString -String 'test' -AsPlainText -Force
        # Use Import-KrCertificate for cross-platform compatibility (doesn't require certificate store)
        $script:clientCert = Import-KrCertificate -FilePath $clientPfx.FullName -Password $password

        if (-not $script:clientCert.HasPrivateKey) {
            throw "Imported client certificate does not include a private key: $($clientPfx.FullName)"
        }

        if (-not (Test-KrCertificate -Certificate $script:clientCert -AllowWeakAlgorithms)) {
            throw "Imported client certificate failed validation: $($clientPfx.FullName)"
        }
    }

    AfterAll {
        # Stop the server
        if ($script:instance) {

            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Returns server info without authentication' {
        $result = Invoke-RestMethod -Uri "$($script:instance.Url)/info" -SkipCertificateCheck
        $result.message | Should -Be 'Client Certificate Authentication Demo'
        $result.endpoints | Should -Not -BeNullOrEmpty
        $result.endpoints.Count | Should -BeGreaterThan 0
    }

    It 'Requires client certificate for /secure/cert/hello' {
        # Attempt without certificate should fail
        {
            Invoke-RestMethod -Uri "$($script:instance.Url)/secure/cert/hello" -SkipCertificateCheck -ErrorAction Stop
        } | Should -Throw
    }

    It 'Authenticates with valid client certificate at /secure/cert/hello' {
        $result = Invoke-RestMethod -Uri "$($script:instance.Url)/secure/cert/hello" `
            -Certificate $script:clientCert `
            -SkipCertificateCheck

        $result.message | Should -Be 'Hello from client certificate authentication'
        $result.subject | Should -Not -BeNullOrEmpty
        $result.issuer | Should -Not -BeNullOrEmpty
        $result.thumbprint | Should -Be $script:clientCert.Thumbprint
        $result.validFrom | Should -Not -BeNullOrEmpty
        $result.validTo | Should -Not -BeNullOrEmpty
    }

    It 'Returns detailed authentication info at /secure/cert/info' {
        $result = Invoke-RestMethod -Uri "$($script:instance.Url)/secure/cert/info" `
            -Certificate $script:clientCert `
            -SkipCertificateCheck

        $result.isAuthenticated | Should -Be $true
        $result.authenticationType | Should -Be 'Certificate'
        $result.name | Should -Not -BeNullOrEmpty
        $result.claims | Should -Not -BeNullOrEmpty
        $result.claims.Count | Should -BeGreaterThan 0

        # Verify certificate information
        $result.certificate.thumbprint | Should -Be $script:clientCert.Thumbprint
        $result.certificate.subject | Should -Not -BeNullOrEmpty
        $result.certificate.issuer | Should -Not -BeNullOrEmpty
        $result.certificate.serialNumber | Should -Not -BeNullOrEmpty

        # Verify expected claims are present
        $claimTypes = $result.claims | ForEach-Object { $_.type }
        $claimTypes | Should -Contain 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'
        $claimTypes | Should -Contain 'thumbprint'
        $claimTypes | Should -Contain 'issuer'
        $claimTypes | Should -Contain 'serialnumber'
    }
}
