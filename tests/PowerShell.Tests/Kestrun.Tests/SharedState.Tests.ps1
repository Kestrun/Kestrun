[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

    $script:testServerName = "SharedStateTests_$([guid]::NewGuid().ToString('N'))"
    $script:testServer = New-KrServer -Name $script:testServerName
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
            } | Should -Throw '*cannot be used when Path is provided*'
        } finally {
            if (Test-Path -LiteralPath $tempRoot) {
                Remove-Item -LiteralPath $tempRoot -Recurse -Force
            }
        }
    }
}


Describe 'Shared state cmdlets' {
    Context 'Set/Get/Remove shared state' {
        It 'sets and gets a server shared-state value' {
            $name = "krTestServer_$([guid]::NewGuid().ToString('N'))"

            Set-KrSharedState -Server $script:testServer -Name $name -Value @{ count = 3; note = 'ok' }
            $value = Get-KrSharedState -Server $script:testServer -Name $name

            $value | Should -Not -BeNullOrEmpty
            $value.count | Should -Be 3
            $value.note | Should -Be 'ok'
        }

        It 'removes a server shared-state value and returns true when it existed' {
            $name = "krTestRemove_$([guid]::NewGuid().ToString('N'))"
            Set-KrSharedState -Server $script:testServer -Name $name -Value @{ value = 42 }

            $removed = Remove-KrSharedState -Server $script:testServer -Name $name -Confirm:$false
            $removed | Should -BeTrue
            (Get-KrSharedState -Server $script:testServer -Name $name) | Should -Be $null
        }

        It 'honors WhatIf when removing global shared-state values' {
            $name = "krTestWhatIf_$([guid]::NewGuid().ToString('N'))"
            try {
                $null = [Kestrun.SharedState.GlobalStore]::Set($name, 'keep-me', $false)
                $result = Remove-KrSharedState -Global -Name $name -WhatIf

                $result | Should -BeFalse
                (Get-KrSharedState -Global -Name $name) | Should -Be 'keep-me'
            } finally {
                $null = [Kestrun.SharedState.GlobalStore]::Remove($name)
            }
        }
    }

    Context 'Export/Import serialization' {
        It 'round-trips an object via string export/import' {
            $inputObject = [ordered]@{
                Name = 'Museum'
                Count = 7
                Tags = @('a', 'b')
            }

            $serialized = Export-KrSharedState -InputObject $inputObject
            $restored = Import-KrSharedState -InputString $serialized

            $restored.Name | Should -Be 'Museum'
            $restored.Count | Should -Be 7
            $restored.Tags | Should -Be @('a', 'b')
        }

        It 'round-trips an object via byte-array export/import' {
            $inputObject = [ordered]@{ A = 1; B = 'two' }

            $bytes = Export-KrSharedState -InputObject $inputObject -OutputType ByteArray
            $restored = Import-KrSharedState -InputBytes $bytes

            $restored.A | Should -Be 1
            $restored.B | Should -Be 'two'
        }

        It 'writes to file and imports from file' {
            $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("kestrun-sharedstate-$([guid]::NewGuid().ToString('N')).xml")
            $inputObject = [ordered]@{ Enabled = $true; Retries = 2 }

            try {
                $fileInfo = Export-KrSharedState -InputObject $inputObject -OutputType File -Path $tempFile
                $fileInfo.FullName | Should -Be $tempFile

                $restored = Import-KrSharedState -Path $tempFile
                $restored.Enabled | Should -BeTrue
                $restored.Retries | Should -Be 2
            } finally {
                Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
            }
        }

        It 'throws when importing from a missing file' {
            $missing = Join-Path ([System.IO.Path]::GetTempPath()) ("kestrun-missing-$([guid]::NewGuid().ToString('N')).xml")

            { Import-KrSharedState -Path $missing } | Should -Throw -ExpectedMessage 'File not found:*'
        }

        It 'throws when file output is selected and Path is empty' {
            { Export-KrSharedState -InputObject @{ A = 1 } -OutputType File -Path '' } | Should -Throw
        }
    }

    Context 'Lock timeout behavior' {
        It 'times out export when provided lock is not available' {
            $key = "krSharedStateExportLock_$([guid]::NewGuid().ToString('N'))"

            {
                Use-KrLock -Key $key -ScriptBlock {
                    $lock = Get-KrLock -Key $key
                    Export-KrSharedState -InputObject @{ A = 1 } -Lock $lock -TimeoutMilliseconds 10
                }
            } | Should -Throw -ExpectedMessage '*Timeout waiting for shared state lock*'
        }

        It 'times out import when provided lock is not available' {
            $key = "krSharedStateImportLock_$([guid]::NewGuid().ToString('N'))"
            $xml = Export-KrSharedState -InputObject @{ B = 2 }

            {
                Use-KrLock -Key $key -ScriptBlock {
                    $lock = Get-KrLock -Key $key
                    Import-KrSharedState -InputString $xml -Lock $lock -TimeoutMilliseconds 10
                }
            } | Should -Throw -ExpectedMessage '*Timeout waiting for shared state lock*'
        }
    }
}
