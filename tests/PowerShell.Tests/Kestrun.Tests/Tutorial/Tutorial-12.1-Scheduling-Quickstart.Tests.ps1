param()
Describe 'Tutorial 12.1 Scheduling Quickstart' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '12.1-Scheduling-Quickstart.ps1'
    }
    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It '/visit increments counter' {
        $base = $script:instance.Url
        $r1 = Invoke-WebRequest -Uri "$base/visit" -UseBasicParsing -TimeoutSec 8
        $r1.StatusCode | Should -Be 200
        $first = [int]([regex]::Match($r1.Content, '(\d+)').Groups[1].Value)

        $r2 = Invoke-WebRequest -Uri "$base/visit" -UseBasicParsing -TimeoutSec 8
        $r2.StatusCode | Should -Be 200
        $second = [int]([regex]::Match($r2.Content, '(\d+)').Groups[1].Value)

        $second | Should -BeGreaterThan $first
    }

    It '/schedule/report contains heartbeat jobs' {
        $base = $script:instance.Url
        $resp = Invoke-WebRequest -Uri "$base/schedule/report" -UseBasicParsing -TimeoutSec 12
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content | ConvertFrom-Json

        # Allow a short delay for first run if not immediate for both
        $names = @($json.jobs | ForEach-Object { $_.name })
        ($names -contains 'heartbeat-ps') | Should -BeTrue
        ($names -contains 'heartbeat-cs') | Should -BeTrue
    }
}
