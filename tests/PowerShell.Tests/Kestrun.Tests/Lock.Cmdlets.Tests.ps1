[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

    $publicLocksPath = Join-Path (Get-ProjectRootDirectory) 'src/PowerShell/Kestrun/Public/Locks'
    . (Join-Path $publicLocksPath 'Get-KrLock.ps1')
    . (Join-Path $publicLocksPath 'Use-KrLock.ps1')
}

Describe 'Lock cmdlets' {
    It 'Get-KrLock returns a lock object' {
        $key = "krLockObj_$([guid]::NewGuid().ToString('N'))"

        $lock = Get-KrLock -Key $key

        $lock | Should -Not -BeNullOrEmpty
        $lock.GetType().FullName | Should -Be 'System.Threading.SemaphoreSlim'
    }

    It 'Get-KrLock returns the same instance for the same key' {
        $key = "krLockSame_$([guid]::NewGuid().ToString('N'))"

        $lock1 = Get-KrLock -Key $key
        $lock2 = Get-KrLock -Key $key

        [object]::ReferenceEquals($lock1, $lock2) | Should -BeTrue
    }

    It 'Use-KrLock executes script block and returns result' {
        $key = "krUseResult_$([guid]::NewGuid().ToString('N'))"

        $result = @(Use-KrLock -Key $key -ScriptBlock {
                return 'ok'
            })

        $result | Should -Contain 'ok'
    }

    It 'Use-KrLock times out when lock is already held' {
        $key = "krUseTimeout_$([guid]::NewGuid().ToString('N'))"
        $lock = Get-KrLock -Key $key

        try {
            $null = $lock.Wait(0)

            {
                Use-KrLock -Key $key -TimeoutMilliseconds 10 -ScriptBlock {
                    return 'should-not-run'
                }
            } | Should -Throw -ExpectedMessage "*Timeout acquiring lock '$key'*"
        } finally {
            try {
                $null = $lock.Release()
            } catch {
                # Ignore release failures when lock was not acquired.
            }
        }
    }

    It 'Use-KrLock releases lock even when script block throws' {
        $key = "krUseThrow_$([guid]::NewGuid().ToString('N'))"
        $lock = Get-KrLock -Key $key

        { Use-KrLock -Key $key -ScriptBlock { throw 'boom' } } | Should -Throw -ExpectedMessage '*boom*'

        $acquired = $lock.Wait(0)
        $acquired | Should -BeTrue

        if ($acquired) {
            $null = $lock.Release()
        }
    }
}
