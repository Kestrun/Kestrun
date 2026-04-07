[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseUsingScopeModifierInNewRunspaces', '')]
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 4.3-Shared-State-Snapshots' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '4.3-Shared-State-Snapshots.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'round-trips shared state through export, reset, and import' {
        $baseUrl = $script:instance.Url

        Invoke-RestMethod -Uri "$baseUrl/visit" -Method Post | Out-Null
        Invoke-RestMethod -Uri "$baseUrl/visit" -Method Post | Out-Null

        $noteBody = @{ note = 'remember helmet' } | ConvertTo-Json
        $noteResponse = Invoke-RestMethod -Uri "$baseUrl/note" -Method Post -Body $noteBody -ContentType 'application/json'
        $noteResponse.notes | Should -Contain 'remember helmet'

        $exportResponse = Invoke-RestMethod -Uri "$baseUrl/snapshot/export" -Method Post
        $exportResponse.snapshot | Should -Match '<Objs'
        $exportResponse.visitCount | Should -Be 2
        $exportResponse.noteCount | Should -Be 1

        $resetResponse = Invoke-RestMethod -Uri "$baseUrl/snapshot/reset" -Method Post
        $resetResponse.message | Should -Be 'State reset'

        $stateAfterReset = Invoke-RestMethod -Uri "$baseUrl/state" -Method Get
        $stateAfterReset.visitCount | Should -Be 0
        @($stateAfterReset.notes).Count | Should -Be 0

        $importBody = @{ snapshot = $exportResponse.snapshot } | ConvertTo-Json -Depth 5
        $importResponse = Invoke-RestMethod -Uri "$baseUrl/snapshot/import" -Method Post -Body $importBody -ContentType 'application/json'
        $importResponse.message | Should -Be 'Snapshot restored'
        $importResponse.visitCount | Should -Be 2
        $importResponse.notes | Should -Contain 'remember helmet'

        $finalState = Invoke-RestMethod -Uri "$baseUrl/state" -Method Get
        $finalState.visitCount | Should -Be 2
        $finalState.snapshotLockKey | Should -Be 'tutorial:shared-state-snapshot'
        $finalState.notes | Should -Contain 'remember helmet'
    }

    It 'returns 400 when importing without a snapshot payload' {
        $baseUrl = $script:instance.Url

        { Invoke-RestMethod -Uri "$baseUrl/snapshot/import" -Method Post -Body '{}' -ContentType 'application/json' -ErrorAction Stop } | Should -Throw

        try {
            Invoke-RestMethod -Uri "$baseUrl/snapshot/import" -Method Post -Body '{}' -ContentType 'application/json' -ErrorAction Stop
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'returns 400 when adding an empty note' {
        $baseUrl = $script:instance.Url

        { Invoke-RestMethod -Uri "$baseUrl/note" -Method Post -Body (@{ note = '' } | ConvertTo-Json) -ContentType 'application/json' -ErrorAction Stop } | Should -Throw

        try {
            Invoke-RestMethod -Uri "$baseUrl/note" -Method Post -Body (@{ note = '' } | ConvertTo-Json) -ContentType 'application/json' -ErrorAction Stop
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }
}
