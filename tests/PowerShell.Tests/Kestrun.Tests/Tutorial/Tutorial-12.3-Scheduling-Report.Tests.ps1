param()
Describe 'Tutorial 12.3 Scheduling Report' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '12.3-Scheduling-Report.ps1'
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It '/schedule/report lists jobs' {
        $base = $script:instance.Url
        $resp = Invoke-WebRequest -Uri "$base/schedule/report" -UseBasicParsing -TimeoutSec 10
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content | ConvertFrom-Json
        $names = @($json.jobs | ForEach-Object { $_.name })
        ($names -contains 'rep-ps') | Should -BeTrue
        ($names -contains 'rep-cs') | Should -BeTrue
    }

    It '/schedule/report supports tz query' {
        $base = $script:instance.Url
        $candidates = @([System.TimeZoneInfo]::Local.Id, 'UTC')
        $ok = $false
        foreach ($tz in $candidates) {
            try {
                $enc = [System.Uri]::EscapeDataString($tz)
                $resp = Invoke-WebRequest -Uri "$base/schedule/report?tz=$enc" -UseBasicParsing -TimeoutSec 10
                if ($resp.StatusCode -eq 200) {
                    $json = $resp.Content | ConvertFrom-Json
                    $json | Should -Not -BeNullOrEmpty
                    $json.generatedAt | Should -Not -BeNullOrEmpty
                    $ok = $true; break
                }
            } catch { 
                # Exception intentionally ignored to allow trying other timezone candidates.
            }
        }
        $ok | Should -BeTrue -Because 'At least one timezone id must be accepted by the server'
    }
}
