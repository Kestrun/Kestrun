param()

Describe 'Example 22.5 nested multipart/mixed' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '22.5-nested-multipart.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path (Split-Path -Parent $script:instance.TempPath) 'uploads'
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
            Stop-ExampleScript -Instance $script:instance
        }
    }

    It 'Parses nested multipart payloads' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $innerBody = @(
            "--$inner",
            'Content-Type: text/plain',
            '',
            'inner-1',
            "--$inner",
            'Content-Type: application/json',
            '',
            '{"nested":true}',
            "--$inner--",
            ''
        ) -join "`r`n"
        $outerBody = @(
            "--$outer",
            'Content-Type: application/json',
            '',
            '{"stage":"outer"}',
            "--$outer",
            "Content-Type: multipart/mixed; boundary=$inner",
            '',
            $innerBody,
            "--$outer--",
            ''
        ) -join "`r`n"

        $resp = Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $outerBody
        $resp.outerCount | Should -Be 2
        $resp.nested[0].nestedCount | Should -Be 2
    }
}
