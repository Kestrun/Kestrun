param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 10.21 - OpenAPI SSE Broadcast (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.21-OpenAPI-SseBroadcast.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'OpenAPI JSON is served and documents broadcast SSE and broadcast APIs' {
        $openApi = "$($script:instance.Url)/openapi/v3.1/openapi.json"
        Assert-RouteContent -Uri $openApi -Contains '/sse/broadcast' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains '/sse/broadcast/progress' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains 'text/event-stream' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains '/api/broadcast' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains '/api/broadcast/progress' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains 'OperationProgressEvent' | Out-Null
    }

    It 'Broadcast API returns ok' {
        $body = @{ event = 'message'; data = @{ text = 'hello' } } | ConvertTo-Json -Compress
        Assert-RouteContent -Uri "$($script:instance.Url)/api/broadcast" -Method Post -ExpectStatus 200 -ContentType 'application/json' -Body $body -JsonField 'ok' -JsonValue 'True' | Out-Null
    }

    It 'Progress broadcast API returns ok' {
        $body = @{ taskId = 'task-1'; progress = 25; status = 'Starting'; state = 'running'; ts = (Get-Date).ToUniversalTime() } | ConvertTo-Json -Compress
        Assert-RouteContent -Uri "$($script:instance.Url)/api/broadcast/progress" -Method Post -ExpectStatus 200 -ContentType 'application/json' -Body $body -JsonField 'ok' -JsonValue 'True' | Out-Null
        Assert-RouteContent -Uri "$($script:instance.Url)/api/broadcast/progress" -Method Post -ExpectStatus 200 -ContentType 'application/json' -Body $body -JsonField 'event' -JsonValue 'progress' | Out-Null
    }
}
