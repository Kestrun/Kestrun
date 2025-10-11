<#
    .SYNOPSIS
        Tests SignalR integration with Kestrun
    .DESCRIPTION
        Validates that SignalR can be configured and the KestrunHub is accessible
#>
param()

Describe 'SignalR Integration' -Tag 'SignalR' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

        $scriptBlock = {
            param(
                [int]$Port = 5000,
                [IPAddress]$IPAddress = [IPAddress]::Loopback
            )
            
            # Setup logging
            New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'SignalRTest' -SetAsDefault
            
            # Create server
            New-KrServer -Name 'SignalR Test Server'
            
            # Add listener
            Add-KrEndpoint -Port $Port -IPAddress $IPAddress -Protocol Http1AndHttp2
            
            # Add SignalR hub
            Add-KrSignalRHubMiddleware -HubType ([Kestrun.SignalR.KestrunHub]) -Path '/testHub'
            
            # Add a test route to check if broadcaster is available
            Add-KrMapRoute -Verbs Get -Pattern '/test/broadcaster' {
                try {
                    $broadcaster = $Server.App.Services.GetService([Kestrun.SignalR.IRealtimeBroadcaster])
                    if ($broadcaster) {
                        Write-KrJsonResponse -InputObject @{ available = $true } -StatusCode 200
                    } else {
                        Write-KrJsonResponse -InputObject @{ available = $false } -StatusCode 200
                    }
                } catch {
                    Write-KrJsonResponse -InputObject @{ available = $false; error = $_.Exception.Message } -StatusCode 500
                }
            }
            
            # Add a test route to broadcast via Send-KrLog
            Add-KrMapRoute -Verbs Get -Pattern '/test/sendlog' {
                try {
                    Send-KrLog -Level Information -Message 'Test log message'
                    Write-KrJsonResponse -InputObject @{ success = $true } -StatusCode 200
                } catch {
                    Write-KrJsonResponse -InputObject @{ success = $false; error = $_.Exception.Message } -StatusCode 500
                }
            }
            
            # Add a test route to broadcast via Send-KrEvent
            Add-KrMapRoute -Verbs Get -Pattern '/test/sendevent' {
                try {
                    Send-KrEvent -EventName 'TestEvent' -Data @{ test = 'data' }
                    Write-KrJsonResponse -InputObject @{ success = $true } -StatusCode 200
                } catch {
                    Write-KrJsonResponse -InputObject @{ success = $false; error = $_.Exception.Message } -StatusCode 500
                }
            }
            
            Enable-KrConfiguration
            Start-KrServer -CloseLogsOnExit
        }
        
        $script:instance = Start-ExampleScript -ScriptBlock $scriptBlock
    }
    
    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
        }
    }
    
    It 'Server starts successfully with SignalR hub configured' {
        $script:instance | Should -Not -BeNullOrEmpty
        $script:instance.Process.HasExited | Should -BeFalse
    }
    
    It 'IRealtimeBroadcaster service is registered' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/test/broadcaster" -SkipHttpErrorCheck
        $response.available | Should -BeTrue
    }
    
    It 'Send-KrLog command works without errors' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/test/sendlog" -SkipHttpErrorCheck
        $response.success | Should -BeTrue
    }
    
    It 'Send-KrEvent command works without errors' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/test/sendevent" -SkipHttpErrorCheck
        $response.success | Should -BeTrue
    }
}
