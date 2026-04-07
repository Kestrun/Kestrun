[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Kestrun Shared State Functions' {
    AfterEach {
        Remove-KrSharedState -Global -Name 'psTestVar' -Confirm:$false -ErrorAction SilentlyContinue | Out-Null
    }

    It 'Set-KrSharedState defines and retrieves values' {
        Set-KrSharedState -Global -Name 'psTestVar' -Value @(1, 2, 3)
        (Get-KrSharedState -Global -Name 'psTestVar').Count | Should -Be 3
    }

    It 'Export-KrSharedState writes a file when Path is provided' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString('N'))
        $tempPath = Join-Path $tempRoot 'state.xml'

        try {
            $result = Export-KrSharedState -InputObject ([pscustomobject]@{ Name = 'demo'; Count = 3 }) -Path $tempPath

            Test-Path -LiteralPath $tempPath | Should -BeTrue
            $result.FullName | Should -Be ([System.IO.Path]::GetFullPath($tempPath))

            $restored = Import-KrSharedState -Path $tempPath
            $restored.Name | Should -Be 'demo'
            $restored.Count | Should -Be 3
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force
            }
        }
    }

    It 'Export-KrSharedState rejects conflicting OutputType when Path is provided' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString('N'))
        $tempPath = Join-Path $tempRoot 'state.xml'

        try {
            {
                Export-KrSharedState -InputObject @{ Name = 'demo' } -Path $tempPath -OutputType String
            } | Should -Throw "*cannot be used when Path is provided*"
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force
            }
        }
    }
}
