param()

Describe 'Tutorial 20.1 - Tasks' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '20.1-Task.ps1' -StartupTimeoutSeconds 45

        # No JSON helpers needed; we work directly with PSCustomObject from Invoke-RestMethod

        function Wait-UntilTaskState {
            param(
                [Parameter(Mandatory)][string]$Id,
                [Parameter(Mandatory)][string[]]$States,
                [int]$TimeoutSec = 20,
                [int]$PollMs = 250
            )
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
            do {
                $path = ('/tasks/state?id={0}' -f [uri]::EscapeDataString($Id))
                if (-not $path.StartsWith('/')) { $path = '/' + $path }
                $url = "$(($script:instance.Url))$path"
                $headers = @{ Accept = 'application/json' }
                $sc = $null
                $params = @{ Uri = $url; TimeoutSec = 12; Headers = $headers; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
                if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
                $obj = Invoke-RestMethod @params
                $sc | Should -Be 200
                if ($null -eq $obj) { return $null }

                if ($obj.StateText -in $States) {
                    return $obj
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
        $sc = $null; $params = @{ Uri = ("$($script:instance.Url)/tasks/create/ps?seconds=1"); Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $create = Invoke-RestMethod @params
        $sc | Should -Be 200
        $create.id | Should -Not -BeNullOrEmpty
        $create.language | Should -Be 'PowerShell'

        # Start
        $startUrl = "$($script:instance.Url)/tasks/start?id=$($create.id)"
        $sc = $null; $params = @{ Uri = $startUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $start = Invoke-RestMethod @params
        $sc | Should -Be 202
        $start.started | Should -BeTrue

        # Wait for completion
        $state = Wait-UntilTaskState -Id $create.id -States @('Completed') -TimeoutSec 25

        $state.id | Should -Be $create.id
        $state.stateText | Should -Be 'Completed'
        $state.StartedAt | Should -Not -BeNullOrEmpty
        $state.CompletedAt | Should -Not -BeNullOrEmpty
        # Result snapshot
        $resultUrl = "$($script:instance.Url)/tasks/result?id=$($create.id)"
        $sc = $null; $params = @{ Uri = $resultUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $result = Invoke-RestMethod @params
        $sc | Should -Be 200

        # Output should include our message
        ($result | Out-String) | Should -Match 'PS task done at'

        # Remove
        $removeUrl = "$($script:instance.Url)/tasks/remove?id=$($create.id)"
        $sc = $null; $params = @{ Uri = $removeUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $removed = Invoke-RestMethod @params
        $sc | Should -Be 200
        $removed.removed | Should -BeTrue
    }

    It 'CSharp task: create → start → complete → result' {
        $sc = $null; $params = @{ Uri = ("$($script:instance.Url)/tasks/create/cs?ms=800"); Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $create = Invoke-RestMethod @params; $sc | Should -Be 200
        $create.id | Should -Not -BeNullOrEmpty
        $create.language | Should -Be 'CSharp'

        $startUrl = "$($script:instance.Url)/tasks/start?id=$($create.id)"
        $sc = $null; $params = @{ Uri = $startUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $start = Invoke-RestMethod @params
        $sc | Should -Be 202
        $start.started | Should -BeTrue

        $state = Wait-UntilTaskState -Id $create.id -States @('Completed') -TimeoutSec 25
        $state.stateText | Should -Be 'Completed'

        $resultUrl = "$($script:instance.Url)/tasks/result?id=$($create.id)"
        $sc = $null; $params = @{ Uri = $resultUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $result = Invoke-RestMethod @params
        $sc | Should -Be 200

        ($result | Out-String) | Should -Match 'CS task done at'
    }

    It 'one-shot run/ps returns 202 and id' {
        $runUrl = "$($script:instance.Url)/tasks/run/ps?seconds=1"
        $sc = $null; $params = @{ Uri = $runUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $r = Invoke-RestMethod @params;
        $sc | Should -Be 202
        $r.started | Should -BeTrue
        $r.id | Should -Not -BeNullOrEmpty
    }

    It 'list returns an array' {
        $sc = $null; $params = @{ Uri = ("$($script:instance.Url)/tasks/list"); Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $list = Invoke-RestMethod @params; $sc | Should -Be 200
        # Accept either empty or non-empty array
        $list.GetType().Name | Should -BeIn @('Object[]', 'PSCustomObject')
    }

    It 'cancel running task transitions to Cancelled or Completed' -Tag 'Slow' {
        # Create a longer PS task so we can cancel
        $sc = $null; $params = @{ Uri = ("$($script:instance.Url)/tasks/create/ps?seconds=5"); Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $create = Invoke-RestMethod @params; $sc | Should -Be 200
        $id = $create.id
        $startUrl = "$($script:instance.Url)/tasks/start?id=$($id)"
        $sc = $null; $params = @{ Uri = $startUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
        if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
        $null = Invoke-RestMethod @params; $sc | Should -Be 202

        # Wait until either Running or Completed; if already Completed, skip cancel
        $pre = Wait-UntilTaskState -Id $id -States @('Running', 'Completed') -TimeoutSec 12
        if ($pre.stateText -ne 'Completed') {
            # Attempt cancel; accept 202 (cancel accepted) or 409 (already finished)
            $cancelUrl = "$($script:instance.Url)/tasks/cancel?id=$($id)"
            $sc = $null; $params = @{ Uri = $cancelUrl; Headers = @{Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
            if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
            $cancel = Invoke-RestMethod @params
            if ($sc -eq 202) {
                $cancel.cancelled | Should -BeTrue
            } elseif ($sc -ne 409) {
                throw "Unexpected status code $sc from cancel"
            }
        }

        # Wait until it reaches a terminal state
        $state = Wait-UntilTaskState -Id $id -States @('Cancelled', 'Completed') -TimeoutSec 25
        $state.stateText | Should -BeIn @('Cancelled', 'Completed')
    }
}
