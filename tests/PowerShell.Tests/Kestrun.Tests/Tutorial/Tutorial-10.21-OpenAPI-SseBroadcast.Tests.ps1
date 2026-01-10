param()

Describe 'Tutorial 10.21 - OpenAPI SSE Broadcast (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '10.21-OpenAPI-SseBroadcast.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'OpenAPI JSON is served and documents broadcast SSE and broadcast API' {
        $openApi = "$($script:instance.Url)/openapi/v3.1/openapi.json"
        Assert-RouteContent -Uri $openApi -Contains '/sse/broadcast' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains 'text/event-stream' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains '/api/broadcast' | Out-Null
    }

    It 'Broadcast API returns ok' {
        $body = @{ event = 'message'; data = @{ text = 'hello' } } | ConvertTo-Json -Compress
        Assert-RouteContent -Uri "$($script:instance.Url)/api/broadcast" -Method Post -ExpectStatus 200 -ContentType 'application/json' -Body $body -JsonField 'ok' -JsonValue 'True' | Out-Null
    }
}
