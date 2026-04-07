[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

    $publicSharedStatePath = Join-Path (Get-ProjectRootDirectory) 'src/PowerShell/Kestrun/Public/SharedState'
    $publicLocksPath = Join-Path (Get-ProjectRootDirectory) 'src/PowerShell/Kestrun/Public/Locks'
    . (Join-Path $publicSharedStatePath 'Export-KrSharedState.ps1')
    . (Join-Path $publicSharedStatePath 'Import-KrSharedState.ps1')
    . (Join-Path $publicLocksPath 'Get-KrLock.ps1')
    . (Join-Path $publicLocksPath 'Use-KrLock.ps1')

    $script:testServerName = "SharedStateTests_$([guid]::NewGuid().ToString('N'))"
    $script:testServer = New-KrServer -Name $script:testServerName
}


AfterAll {
    if ($script:testServerName) {
        Remove-KrServer -Name $script:testServerName -Force -ErrorAction SilentlyContinue
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
            $input = [ordered]@{
                Name = 'Museum'
                Count = 7
                Tags = @('a', 'b')
            }

            $serialized = Export-KrSharedState -InputObject $input
            $restored = Import-KrSharedState -InputString $serialized

            $restored.Name | Should -Be 'Museum'
            $restored.Count | Should -Be 7
            $restored.Tags | Should -Be @('a', 'b')
        }

        It 'round-trips an object via byte-array export/import' {
            $input = [ordered]@{ A = 1; B = 'two' }

            $bytes = Export-KrSharedState -InputObject $input -OutputType ByteArray
            $restored = Import-KrSharedState -InputBytes $bytes

            $restored.A | Should -Be 1
            $restored.B | Should -Be 'two'
        }

        It 'writes to file and imports from file' {
            $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("kestrun-sharedstate-$([guid]::NewGuid().ToString('N')).xml")
            $input = [ordered]@{ Enabled = $true; Retries = 2 }

            try {
                $fileInfo = Export-KrSharedState -InputObject $input -OutputType File -Path $tempFile
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
