param()
Describe 'Example 3.5-File-ServerCaching' {
    BeforeAll {. (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '3.5-File-ServerCaching.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Returns index.html with caching headers' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/index.html" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content -like '*<html*') | Should -BeTrue
        $resp.Headers['Cache-Control'] | Should -Match 'public'
        $resp.Headers['Cache-Control'] | Should -Match 'max-age=300'
    }

    It 'Second request reuses same Cache-Control header' {
        $first = Invoke-WebRequest -Uri "$($script:instance.Url)/index.html" -UseBasicParsing -TimeoutSec 6 -Method Get
        Start-Sleep -Milliseconds 120
        $second = Invoke-WebRequest -Uri "$($script:instance.Url)/index.html" -UseBasicParsing -TimeoutSec 6 -Method Get
        $first.StatusCode | Should -Be 200
        $second.StatusCode | Should -Be 200
        $first.Headers['Cache-Control'] | Should -Be $second.Headers['Cache-Control']
    }
}
