[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Kestrun PowerShell Functions' {
    AfterAll {
        Remove-Variable Response -Scope Script -ErrorAction SilentlyContinue
    }

    It 'Set-KrSharedState defines and retrieves values' {
        Set-KrSharedState -Name 'psTestVar' -Value @(1, 2, 3)
        (Get-KrSharedState -Name 'psTestVar').Count | Should -Be 3
    }

    It 'Resolve-KrPath returns absolute path' {
        $result = Resolve-KrPath -Path '.' -KestrunRoot
        [System.IO.Path]::IsPathRooted($result) | Should -BeTrue
    }

    It 'Resolve-KrPath -Test returns original when file missing' {
        $missing = 'DefinitelyMissing_KrTestFile_12345.ps1'
        (Test-Path $missing) | Should -BeFalse
        $result = Resolve-KrPath -Path $missing -KestrunRoot -Test
        $result | Should -Be $missing
    }

    It 'Resolve-KrPath normalizes mixed separators and dot segments' {
        $sampleRelative = './subdir/..'  # should collapse to base root when combined
        $base = (Resolve-KrPath -Path '.' -KestrunRoot)
        $result = Resolve-KrPath -Path $sampleRelative -RelativeBasePath $base
        [System.IO.Path]::IsPathRooted($result) | Should -BeTrue
        # Should not contain '/./' or '\\.' sequences
        $result -match '/\./|\\\.' | Should -BeFalse
    }

    <# It 'Write-KrTextResponse calls method on Response object' {
        $called = $null
        $Context.Response = [pscustomobject]@{
            WriteTextResponse = { param($o, $s, $c) $called = "$o|$s|$c" }
        }
        Write-KrTextResponse -InputObject 'hi' -StatusCode 201 -ContentType 'text/plain'
        $script:called | Should -Be 'hi|201|text/plain'
        Remove-Variable Response -Scope Script
    }#>
}
