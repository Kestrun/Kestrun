param()

Describe 'Tutorial 10.5 - SignalR (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')

        # Start the SignalR example server
        $script:instance = Start-ExampleScript -Name '10.5-SignalR.ps1' -StartupTimeoutSeconds 60

        # Helper: wait until operation completed via status endpoint
        function Wait-UntilOperationCompleted {
            param(
                [Parameter(Mandatory)][string]$Id,
                [int]$TimeoutSec = 30,
                [int]$PollMs = 300
            )
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
            do {
                $url = "$($script:instance.Url)/api/operation/status/$Id"
                $params = @{ Uri = $url; TimeoutSec = 12; Headers = @{ Accept = 'application/json' }; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
                if ($script:instance.Https) { $params.SkipCertificateCheck = $true }
                $sc = $null
                $obj = Invoke-RestMethod @params
                $sc | Should -Be 200

                if ($null -ne $obj -and $obj.State -eq 'Completed') { return $obj }
                Start-Sleep -Milliseconds $PollMs
            } while ([DateTime]::UtcNow -lt $deadline)
            throw "Operation '$Id' did not reach Completed state within ${TimeoutSec}s."
        }
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Home page is served' {
        Assert-RouteContent -Uri "$($script:instance.Url)/" -Contains 'SignalR' | Out-Null
    }

    It 'Broadcasts a log and a custom event to the hub' {
        # Capture hub messages for ReceiveLog, after we trigger the route post-handshake
        $logUrl = "$($script:instance.Url)/api/ps/log/Information"
        $msgs = Get-SignalRMessages -BaseUrl $script:instance.Url -Count 8 -TimeoutSeconds 20 -OnConnected { param($url) Assert-RouteContent -Uri $url -Contains 'Broadcasted' | Out-Null } -OnConnectedArg $logUrl
        $msgs | Should -Not -BeNullOrEmpty
        $msg = $msgs | Where-Object { $_.Target -eq 'ReceiveLog' } | Select-Object -First 1
        $msg | Should -Not -BeNullOrEmpty -Because 'Expected at least one ReceiveLog message'
        $arg = $msg.Arguments[0]
        ($arg.level ?? $arg.Level) | Should -Be 'Information'

        # Capture hub messages for ReceiveEvent with on-connected trigger
        $evtUrl = "$($script:instance.Url)/api/ps/event"
        $events = Get-SignalRMessages -BaseUrl $script:instance.Url -Count 8 -TimeoutSeconds 20 -OnConnected {
            param($url)
            Assert-RouteContent -Uri $url -Contains 'Broadcasted custom event' | Out-Null
        } -OnConnectedArg $evtUrl
        $events | Should -Not -BeNullOrEmpty
        $ev = $events | Where-Object { $_.Target -eq 'ReceiveEvent' } | Select-Object -First 1
        $ev | Should -Not -BeNullOrEmpty
        $payload = $ev.Arguments[0]
        ($payload.EventName ?? $payload.eventName) | Should -Be 'PowerShellEvent'
        $data = $payload.Data ?? $payload.data
        $data | Should -Not -BeNullOrEmpty
    }

    It 'Long operation emits progress and completes (100%)' -Tag 'Slow' {
        # Listen for hub events and start the operation after the WebSocket handshake
        $ctx = @{ Url = $script:instance.Url; Https = $script:instance.Https; Seconds = 4; Id = $null }
        $msgs = Get-SignalRMessages -BaseUrl $script:instance.Url -Count 20 -TimeoutSeconds 60 -OnConnected {
            param($c)
            $startUrl = "$($c.Url)/api/operation/start?seconds=$($c.Seconds)"
            $sc = $null
            $params = @{ Uri = $startUrl; Method = 'Post'; Headers = @{ Accept = 'application/json' }; TimeoutSec = 12; SkipHttpErrorCheck = $true; StatusCodeVariable = 'sc' }
            if ($c.Https) { $params.SkipCertificateCheck = $true }
            $resp = Invoke-RestMethod @params
            if ($sc -ne 200) { throw "Start operation failed with status $sc" }
            $c.Id = ($resp.TaskId ?? $resp.taskId ?? $resp.Id ?? $resp.id)
        } -OnConnectedArg $ctx
        $msgs | Should -Not -BeNullOrEmpty

        $progressEvents = @()
        $completeEvents = @()
        foreach ($m in $msgs) {
            if ($m.Target -ne 'ReceiveEvent' -or -not $m.Arguments -or $m.Arguments.Count -lt 1) { continue }
            $p = $m.Arguments[0]
            $name = $p.EventName ?? $p.eventName
            if (-not $name) { continue }
            $data = $p.Data ?? $p.data
            if ($name -eq 'OperationProgress') { $progressEvents += $data }
            elseif ($name -eq 'OperationComplete') { $completeEvents += $data }
        }

        $progressEvents.Count | Should -BeGreaterThan 0 -Because 'Should receive at least one progress event'
        $completeEvents.Count | Should -BeGreaterThan 0 -Because 'Should receive completion event'

        # Verify final status via API
        $ctx.Id | Should -Not -BeNullOrEmpty
        $st = Wait-UntilOperationCompleted -Id $ctx.Id -TimeoutSec 40
        $st.State | Should -Be 'Completed'
        [int]($st.Progress ?? 0) | Should -BeGreaterThanOrEqual 100
    }
}
