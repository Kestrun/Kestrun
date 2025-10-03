param()
Describe 'Tutorial 12.2 Scheduling Cron' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '12.2-Scheduling-Cron.ps1'
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It '/schedule/report lists cron jobs' {
        $base = $script:instance.Url
        $resp = Invoke-WebRequest -Uri "$base/schedule/report" -UseBasicParsing -TimeoutSec 10
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content | ConvertFrom-Json
        $names = @($json.jobs | ForEach-Object { $_.name })
        ($names -contains 'cron-ps') | Should -BeTrue
        ($names -contains 'cron-cs') | Should -BeTrue
    }
}
