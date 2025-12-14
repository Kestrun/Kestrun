param()

Describe 'Tutorial 15.5 - SignalR (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')

        # Start the SignalR example server
        $script:instance = Start-ExampleScript -Name '15.5-SignalR.ps1' -StartupTimeoutSeconds 60

        <#
        .SYNOPSIS
            Waits until the specified operation reaches the Completed state or times out.
        .DESCRIPTION
            This function polls the operation status endpoint for the given operation ID until the operation state is Completed or the timeout is reached.
        .PARAMETER Id
            The ID of the operation to monitor.
        .PARAMETER TimeoutSec
            The maximum time in seconds to wait for the operation to complete. Default is 30 seconds.
        .PARAMETER PollMs
            The interval in milliseconds between status checks. Default is 300 ms.
        .OUTPUTS
            The operation status object when the operation reaches the Completed state.
        .EXAMPLE
            Wait-UntilOperationCompleted -Id "operation-id" -TimeoutSec 60
            Waits up to 60 seconds for the operation with ID "operation-id" to reach the Completed state.
        .NOTES
            This function is intended for use in tests to monitor long-running operations.
        #>
        function Wait-UntilOperationCompleted {
            param(
                [Parameter(Mandatory)][string]$Id,
                [int]$TimeoutSec = 30,
                [int]$PollMs = 300
            )
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
            do {
                $url = "$($script:instance.Url)/api/operation/status/$Id"
                $invokeParams = @{ Uri = $url; Method = 'Get'; UseBasicParsing = $true; TimeoutSec = 12; Headers = @{ Accept = 'application/json' } }
                if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }
                try {
                    $resp = Invoke-WebRequest @invokeParams
                    if ($resp.StatusCode -eq 200) {
                        $obj = $resp.Content | ConvertFrom-Json -ErrorAction Stop
                        if ($null -ne $obj -and $obj.State -eq 'Completed') { return $obj }
                    }
                } catch {
                    # ignore transient issues and keep polling
                    Write-Debug "Error checking operation status: $_"
                }
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
        $msgs = Get-SignalRMessage -BaseUrl $script:instance.Url -Count 2 -TimeoutSeconds 12 -OnConnected { param($url) Assert-RouteContent -Uri $url -Contains 'Broadcasted' | Out-Null } -OnConnectedArg $logUrl
        $msgs | Should -Not -BeNullOrEmpty
        $msg = $msgs | Where-Object { $_.Target -eq 'ReceiveLog' } | Select-Object -First 1
        $msg | Should -Not -BeNullOrEmpty -Because 'Expected at least one ReceiveLog message'
        $arg = $msg.Arguments[0]
        ($arg.level ?? $arg.Level) | Should -Be 'Information'

        # Capture hub messages for ReceiveEvent with on-connected trigger
        $evtUrl = "$($script:instance.Url)/api/ps/event"
        $events = Get-SignalRMessage -BaseUrl $script:instance.Url -Count 3 -TimeoutSeconds 15 -OnConnected {
            param($url)
            Assert-RouteContent -Uri $url -Contains 'Broadcasted custom event' | Out-Null
        } -OnConnectedArg $evtUrl
        $events | Should -Not -BeNullOrEmpty
        $evCandidates = $events | Where-Object { $_.Target -eq 'ReceiveEvent' }
        $evCandidates | Should -Not -BeNullOrEmpty -Because 'Expected ReceiveEvent messages'
        $psEvent = $null
        foreach ($m in $evCandidates) {
            if ($m.Arguments -and $m.Arguments.Count -gt 0) {
                $pl = $m.Arguments[0]
                $ename = $pl.EventName ?? $pl.eventName
                if ($ename -eq 'PowerShellEvent') { $psEvent = $pl; break }
            }
        }
        $psEvent | Should -Not -BeNullOrEmpty -Because 'Expected a PowerShellEvent within ReceiveEvent messages'
        ($psEvent.Data ?? $psEvent.data) | Should -Not -BeNullOrEmpty
    }

    It 'Long operation emits progress and completes (100%)' -Tag 'Slow' {
        # Start the operation via API and verify completion using the status endpoint
        $startUrl = "$($script:instance.Url)/api/operation/start?seconds=4"
        $invokeParams = @{ Uri = $startUrl; Method = 'Post'; UseBasicParsing = $true; TimeoutSec = 12; Headers = @{ Accept = 'application/json' } }
        if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }
        $startResp = Invoke-WebRequest @invokeParams
        $startResp.StatusCode | Should -Be 200
        $respObj = $startResp.Content | ConvertFrom-Json
        $id = ($respObj.TaskId ?? $respObj.taskId ?? $respObj.Id ?? $respObj.id)
        $id | Should -Not -BeNullOrEmpty

        $st = Wait-UntilOperationCompleted -Id $id -TimeoutSec 40
        $st.State | Should -Be 'Completed'
        [int]($st.Progress ?? 0) | Should -BeGreaterOrEqual 100
    }
}

