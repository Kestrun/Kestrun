param()

Describe 'Tutorial 20.1 - Tasks' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '20.1-Task.ps1' -StartupTimeoutSeconds 45

        function Invoke-TasksJson {
            param(
                [Parameter(Mandatory)][string]$Path,
                [int]$ExpectStatus = 200
            )
            if (-not $Path.StartsWith('/')) { $Path = '/' + $Path }
            $url = "$($script:instance.Url)$Path"
            $headers = @{ Accept = 'application/json' }
            $params = @{ Uri = $url; UseBasicParsing = $true; TimeoutSec = 12; Headers = $headers }
            if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
            $resp = Invoke-WebRequest @params
            $resp.StatusCode | Should -Be $ExpectStatus
            return ($resp.Content | ConvertFrom-Json)
        }

        function Wait-UntilTaskState {
            param(
                [Parameter(Mandatory)][string]$Id,
                [Parameter(Mandatory)][string[]]$States,
                [int]$TimeoutSec = 20,
                [int]$PollMs = 250
            )
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
            do {
                $uri = "$($script:instance.Url)/tasks/state?id={0}" -f [uri]::EscapeDataString($Id)
                $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing `
                    -TimeoutSec 12 -SkipCertificateCheck `
                    -SkipHttpErrorCheck -Headers @{ Accept = 'application/json' }
                $resp.StatusCode | Should -Be 200
                $resp.Content | Should -Not -BeNullOrEmpty
                $obj = $resp.Content | ConvertFrom-Json -AsHashtable

                # /tasks/state returns: { id, state } where state may be an enum numeric or string
                $st = $obj.state
                if ($st -is [int] -or $st -is [long] -or ($st -is [string] -and $st -match '^[0-9]+$')) {
                    $st = [int]$st
                    switch ([int]$st) {
                        0 { $st = 'Created' }
                        1 { $st = 'Running' }
                        2 { $st = 'Completed' }
                        3 { $st = 'Faulted' }
                        4 { $st = 'Cancelled' }
                    }
                }

                Start-Sleep -Milliseconds $PollMs
            } while ([DateTime]::UtcNow -lt $deadline)
            throw "Task '$Id' did not reach any of states [$($States -join ', ')] within ${TimeoutSec}s. Last: $($obj.state)"
        }
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It '/hello responds with greeting' {
        # For text route, Invoke-TasksJson would attempt ConvertFrom-Json and throw.
        # Use Assert-RouteContent for this one.
        Assert-RouteContent -Uri "$($script:instance.Url)/hello" -Contains 'Hello, Tasks World!'
    }

    It 'PowerShell task: create → start → complete → result → remove' {
        # Create
        $create = Invoke-TasksJson -Path '/tasks/create/ps?seconds=1'
        $create.id | Should -Not -BeNullOrEmpty
        $create.language | Should -Be 'PowerShell'

        # Start
        $start = Invoke-TasksJson -Path ('$res{0}' -f [uri]::EscapeDataString($create.id)) -ExpectStatus 202
        $start.started | Should -BeTrue

        # Wait for completion
        $state = Wait-UntilTaskState -Id $create.id -States @('Completed') -TimeoutSec 25
        $state.stateText | Should -Be 'Completed'

        # Result snapshot
        $result = Invoke-TasksJson -Path ('/tasks/result?id={0}' -f [uri]::EscapeDataString($create.id))
        $result.id | Should -Be $create.id
        $result.stateText | Should -Be 'Completed'
        $result.startedAt | Should -Not -BeNullOrEmpty
        $result.completedAt | Should -Not -BeNullOrEmpty
        # Output should include our message
        ($result.output | Out-String) | Should -Match 'PS task done at'

        # Remove
        $removed = Invoke-TasksJson -Path ('/tasks/remove?id={0}' -f [uri]::EscapeDataString($create.id))
        $removed.removed | Should -BeTrue
    }

    It 'CSharp task: create → start → complete → result' {
        $create = Invoke-TasksJson -Path '/tasks/create/cs?ms=800'
        $create.id | Should -Not -BeNullOrEmpty
        $create.language | Should -Be 'CSharp'

        $start = Invoke-TasksJson -Path ('/tasks/start?id={0}' -f [uri]::EscapeDataString($create.id)) -ExpectStatus 202
        $start.started | Should -BeTrue

        $state = Wait-UntilTaskState -Id $create.id -States @('Completed') -TimeoutSec 25
        $state.stateText | Should -Be 'Completed'

        $result = Invoke-TasksJson -Path ('/tasks/result?id={0}' -f [uri]::EscapeDataString($create.id))
        $result.stateText | Should -Be 'Completed'
        ($result.output | Out-String) | Should -Match 'CS task done at'
    }

    It 'one-shot run/ps returns 202 and id' {
        $r = Invoke-TasksJson -Path '/tasks/run/ps?seconds=1' -ExpectStatus 202
        $r.started | Should -BeTrue
        $r.id | Should -Not -BeNullOrEmpty
    }

    It 'list returns an array' {
        $list = Invoke-TasksJson -Path '/tasks/list'
        # Accept either empty or non-empty array
        $list.GetType().Name | Should -BeIn @('Object[]', 'PSCustomObject')
    }

    It 'cancel running task transitions to Cancelled or Completed' -Tag 'Slow' {
        # Create a longer PS task so we can cancel
        $create = Invoke-TasksJson -Path '/tasks/create/ps?seconds=5'
        $id = $create.id
        $null = Invoke-TasksJson -Path ('/tasks/start?id={0}' -f [uri]::EscapeDataString($id)) -ExpectStatus 202

        # Wait until either Running or Completed; if already Completed, skip cancel
        $pre = Wait-UntilTaskState -Id $id -States @('Running', 'Completed') -TimeoutSec 12
        if ($pre.stateText -ne 'Completed') {
            # Attempt cancel; accept 202 (cancel accepted) or 409 (already finished)
            try {
                $cancel = Invoke-TasksJson -Path ('/tasks/cancel?id={0}' -f [uri]::EscapeDataString($id)) -ExpectStatus 202
                $cancel.cancelled | Should -BeTrue
            } catch {
                if ($_.Exception.Message -notmatch '409') { throw }
            }
        }

        # Wait until it reaches a terminal state
        $state = Wait-UntilTaskState -Id $id -States @('Cancelled', 'Completed') -TimeoutSec 25
        $state.stateText | Should -BeIn @('Cancelled', 'Completed')
    }
}
