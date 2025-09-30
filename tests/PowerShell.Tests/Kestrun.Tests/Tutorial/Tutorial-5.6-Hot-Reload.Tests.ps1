param()

Describe 'Example 5.6-Hot-Reload' -Tag 'Tutorial', 'Logging', 'HotReload' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.6-Hot-Reload.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /log emits ok with current level' {
        try {
            $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/log" -UseBasicParsing -TimeoutSec 8 -Method Get -ErrorAction Stop
        } catch {
            $_.Exception | Out-String | Write-Host
            throw
        }
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '^ok - '
    }

    It 'GET /level/Warning updates level switch' {
        try {
            $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/level/Warning" -UseBasicParsing -TimeoutSec 8 -Method Get -ErrorAction Stop
        } catch {
            $_.Exception | Out-String | Write-Host
            throw
        }
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'level=Warning'
    }

    It 'GET /level/Invalid returns error (400 BadRequest)' {
        { Invoke-WebRequest -Uri "$($script:instance.Url)/level/NotALevel" -UseBasicParsing -TimeoutSec 8 -Method Get -ErrorAction Stop } | Should -Throw
    }
}
