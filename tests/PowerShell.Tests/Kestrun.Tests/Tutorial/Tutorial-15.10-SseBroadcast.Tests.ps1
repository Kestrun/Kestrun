param()

Describe 'Tutorial 15.10 - SSE Broadcast (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '15.10-SseBroadcast.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Home page is served' {
        Assert-RouteContent -Uri "$($script:instance.Url)/" -Contains 'SSE Broadcast' | Out-Null
    }

    It 'Broadcast API returns ok' {
        $body = @{ event = 'message'; data = @{ text = 'hello' } } | ConvertTo-Json -Compress
        Assert-RouteContent -Uri "$($script:instance.Url)/api/broadcast" -Method Post -ExpectStatus 200 -ContentType 'application/json' -Body $body -JsonField 'ok' -JsonValue 'True' | Out-Null
    }

    It 'Progress broadcast API returns ok' {
        $body = @{ taskId = 'task-1'; progress = 50; status = 'Halfway'; state = 'running' } | ConvertTo-Json -Compress
        Assert-RouteContent -Uri "$($script:instance.Url)/api/broadcast/progress" -Method Post -ExpectStatus 200 -ContentType 'application/json' -Body $body -JsonField 'ok' -JsonValue 'True' | Out-Null
        Assert-RouteContent -Uri "$($script:instance.Url)/api/broadcast/progress" -Method Post -ExpectStatus 200 -ContentType 'application/json' -Body $body -JsonField 'event' -JsonValue 'progress' | Out-Null
    }
}
