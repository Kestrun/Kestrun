param()

Describe 'Example 22.3 Urlencoded forms' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '22.3-urlencoded.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path (Split-Path -Parent $script:instance.TempPath) 'uploads'
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
            Stop-ExampleScript -Instance $script:instance
        }
    }

    It 'Returns parsed urlencoded fields' {
        $body = 'name=Kestrun&role=admin&role=maintainer'
        $resp = Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/form" -ContentType 'application/x-www-form-urlencoded' -Body $body
        $resp.fields.name[0] | Should -Be 'Kestrun'
        $resp.fields.role.Count | Should -Be 2
    }
}
