[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    Import-Module (Get-KestrunModulePath) -Force
}

Describe 'Test-KrCertificate' {
    It 'accepts an explicit root chain for development certificates' {
        $modulePath = (Get-KestrunModulePath).Replace("'", "''")
        $powerShellPath = (Get-Process -Id $PID).Path
        $tempFile = New-TemporaryFile
        $tempScriptPath = [System.IO.Path]::ChangeExtension($tempFile.FullName, '.ps1')
        Move-Item -LiteralPath $tempFile.FullName -Destination $tempScriptPath -Force

        try {
            $scriptContent = @'
Import-Module '__MODULE_PATH__' -Force

$bundle = $null

try {
    $bundle = New-KrDevelopmentCertificate -Exportable

    $withoutChainReason = '__unset__'
    $withoutChainIsValid = Test-KrCertificate -Certificate $bundle.LeafCertificate -FailureReasonVariable 'withoutChainReason'

    $withChainReason = '__unset__'
    $withChainIsValid = Test-KrCertificate -Certificate $bundle.LeafCertificate -CertificateChain $bundle.RootCertificate -FailureReasonVariable 'withChainReason'

    [pscustomobject]@{
        WithoutChainIsValid = $withoutChainIsValid
        WithoutChainReason = $withoutChainReason
        WithChainIsValid = $withChainIsValid
        WithChainReason = $withChainReason
    } | ConvertTo-Json -Compress
}
finally {
    if ($bundle) {
        if ($bundle.LeafCertificate) { $bundle.LeafCertificate.Dispose() }
        if ($bundle.PublicRootCertificate) { $bundle.PublicRootCertificate.Dispose() }
        if ($bundle.RootCertificate) { $bundle.RootCertificate.Dispose() }
    }
}
'@

            $scriptContent = $scriptContent.Replace('__MODULE_PATH__', $modulePath)

            [System.IO.File]::WriteAllText($tempScriptPath, $scriptContent, [System.Text.UTF8Encoding]::new($false))

            $result = & $powerShellPath -NoLogo -NoProfile -File $tempScriptPath | ConvertFrom-Json
            $result.WithoutChainIsValid | Should -BeFalse
            $result.WithoutChainReason | Should -Not -Be '__unset__'
            $result.WithoutChainReason | Should -Not -BeNullOrEmpty
            $result.WithChainIsValid | Should -BeTrue
            $result.WithChainReason | Should -Be ''
        } finally {
            Remove-Item -LiteralPath $tempScriptPath -Force -ErrorAction SilentlyContinue
        }
    }
}
