param()

Describe 'Tutorial 10.17 - OpenAPI SignalR (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '10.17-OpenAPI-SignalR.ps1' -StartupTimeoutSeconds 60
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'OpenAPI JSON is served and documents SignalR-adjacent routes' {
        $openApi = "$($script:instance.Url)/openapi/v3.1/openapi.json"
        Assert-RouteContent -Uri $openApi -Contains '/api/ps/log/{level}' | Out-Null
    }

    It 'Log broadcast route responds' {
        Assert-RouteContent -Uri "$($script:instance.Url)/api/ps/log/Information" -Contains 'Broadcasted' | Out-Null
    }
}
