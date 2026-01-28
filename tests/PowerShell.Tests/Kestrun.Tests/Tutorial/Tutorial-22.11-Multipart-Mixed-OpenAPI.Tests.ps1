param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}


Describe 'Example 22.11 multipart/mixed ordered parts using OpenAPI' -Tag 'Tutorial', 'multipart/form', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '22.11-Multipart-Mixed-OpenAPI.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $script:instance.BaseName
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
            Stop-ExampleScript -Instance $script:instance
        }
    }

    It 'Returns ordered part count and content types' {
        $boundary = 'mixed-boundary'
        $body = @(
            "--$boundary",
            'Content-Type: text/plain',
            '',
            'first',
            "--$boundary",
            'Content-Type: application/json',
            '',
            '{"value":42}',
            "--$boundary--",
            ''
        ) -join "`r`n"

        $resp = Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/mixed" -ContentType "multipart/mixed; boundary=$boundary" -Body $body
        $resp.count | Should -Be 2
        $resp.contentTypes[0] | Should -Be 'text/plain'
        $resp.contentTypes[1] | Should -Be 'application/json'
    }
}
