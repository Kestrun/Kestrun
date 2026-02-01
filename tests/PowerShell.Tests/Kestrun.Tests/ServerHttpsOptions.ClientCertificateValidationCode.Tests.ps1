[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingEmptyCatchBlock', '')]
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}


Describe 'Set-KrServerHttpsOptions -ClientCertificateValidationCode' {

    BeforeEach {
        $script:serverName = 'TlsClientCertValidationCode'
        New-KrServer -Name $script:serverName -Default -Force | Out-Null
        $script:server = [Kestrun.KestrunHostManager]::Default
        $script:server | Should -Not -BeNullOrEmpty
    }

    AfterEach {
        try {
            Remove-KrServer -Name $script:serverName -Force | Out-Null
        } catch {
            # best-effort cleanup
        }
    }

    It 'compiles C# code into the TLS callback delegate' {
        $code = @'
if (certificate is null) return true;
return sslPolicyErrors == SslPolicyErrors.None
    || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors;
'@

        Set-KrServerHttpsOptions -Server $script:server `
            -ClientCertificateMode AllowCertificate `
            -ClientCertificateValidationLanguage CSharp `
            -ClientCertificateValidationCode $code | Out-Null

        $cb = $script:server.Options.HttpsConnectionAdapter.ClientCertificateValidation
        $cb | Should -Not -BeNullOrEmpty

        # Validate callable + expected behavior
        $cb.Invoke($null, $null, [System.Net.Security.SslPolicyErrors]::RemoteCertificateChainErrors) | Should -BeTrue
        $cb.Invoke($null, $null, [System.Net.Security.SslPolicyErrors]::RemoteCertificateNameMismatch) | Should -BeTrue
    }

    It 'compiles VB.NET code into the TLS callback delegate' {
        $code = @'
If certificate Is Nothing Then
    Return True
End If

Return sslPolicyErrors = SslPolicyErrors.None OrElse sslPolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors
'@

        Set-KrServerHttpsOptions -Server $script:server `
            -ClientCertificateMode AllowCertificate `
            -ClientCertificateValidationLanguage VBNet `
            -ClientCertificateValidationCode $code | Out-Null

        $cb = $script:server.Options.HttpsConnectionAdapter.ClientCertificateValidation
        $cb | Should -Not -BeNullOrEmpty

        $cb.Invoke($null, $null, [System.Net.Security.SslPolicyErrors]::RemoteCertificateChainErrors) | Should -BeTrue
    }

    It 'compiles C# code from file path and infers language from .cs extension' {
        $path = Join-Path $TestDrive 'client-cert-validation.cs'
        Set-Content -LiteralPath $path -Value @'
if (certificate is null) return true;
return sslPolicyErrors == SslPolicyErrors.None
    || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors;
'@ -Encoding UTF8

        Set-KrServerHttpsOptions -Server $script:server `
            -ClientCertificateMode AllowCertificate `
            -ClientCertificateValidationCodePath $path | Out-Null

        $cb = $script:server.Options.HttpsConnectionAdapter.ClientCertificateValidation
        $cb | Should -Not -BeNullOrEmpty
        $cb.Invoke($null, $null, [System.Net.Security.SslPolicyErrors]::RemoteCertificateChainErrors) | Should -BeTrue
    }

    It 'compiles VB.NET code from file path and infers language from .vb extension' {
        $path = Join-Path $TestDrive 'client-cert-validation.vb'
        Set-Content -LiteralPath $path -Value @'
If certificate Is Nothing Then
    Return True
End If

Return sslPolicyErrors = SslPolicyErrors.None OrElse sslPolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors
'@ -Encoding UTF8

        Set-KrServerHttpsOptions -Server $script:server `
            -ClientCertificateMode AllowCertificate `
            -ClientCertificateValidationCodePath $path | Out-Null

        $cb = $script:server.Options.HttpsConnectionAdapter.ClientCertificateValidation
        $cb | Should -Not -BeNullOrEmpty
        $cb.Invoke($null, $null, [System.Net.Security.SslPolicyErrors]::RemoteCertificateChainErrors) | Should -BeTrue
    }

    It 'rejects unsupported script languages' {
        { Set-KrServerHttpsOptions -Server $script:server -ClientCertificateValidationLanguage PowerShell -ClientCertificateValidationCode 'return true;' } | Should -Throw
    }

    It 'rejects specifying language when using code path (language inferred)' {
        $path = Join-Path $TestDrive 'client-cert-validation.cs'
        Set-Content -LiteralPath $path -Value 'return true;' -Encoding UTF8
        { Set-KrServerHttpsOptions -Server $script:server -ClientCertificateValidationLanguage CSharp -ClientCertificateValidationCodePath $path } | Should -Throw
    }

    It 'rejects specifying both code and code path' {
        $path = Join-Path $TestDrive 'client-cert-validation.cs'
        Set-Content -LiteralPath $path -Value 'return true;' -Encoding UTF8
        { Set-KrServerHttpsOptions -Server $script:server -ClientCertificateValidationCode 'return true;' -ClientCertificateValidationCodePath $path } | Should -Throw
    }

    It 'rejects specifying both delegate and code' {
        $code = 'return true;'
        $delegate = [System.Func[
        System.Security.Cryptography.X509Certificates.X509Certificate2,
        System.Security.Cryptography.X509Certificates.X509Chain,
        System.Net.Security.SslPolicyErrors,
        bool
        ]] { param($c, $ch, $e) $true }

        { Set-KrServerHttpsOptions -Server $script:server -ClientCertificateValidationCode $code -ClientCertificateValidation $delegate } | Should -Throw
    }

    It 'rejects specifying both delegate and code path' {
        $path = Join-Path $TestDrive 'client-cert-validation.cs'
        Set-Content -LiteralPath $path -Value 'return true;' -Encoding UTF8

        $delegate = [System.Func[
        System.Security.Cryptography.X509Certificates.X509Certificate2,
        System.Security.Cryptography.X509Certificates.X509Chain,
        System.Net.Security.SslPolicyErrors,
        bool
        ]] { param($c, $ch, $e) $true }

        { Set-KrServerHttpsOptions -Server $script:server -ClientCertificateValidationCodePath $path -ClientCertificateValidation $delegate } | Should -Throw
    }
}
