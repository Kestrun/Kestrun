param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 22.4 multipart/mixed ordered parts' -Tag 'Tutorial', 'multipart/form', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '22.4-Multipart-Mixed.ps1'
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
            'Content-Disposition: form-data; name="text"',
            'Content-Type: text/plain',
            '',
            'first',
            "--$boundary",
            'Content-Disposition: form-data; name="json"',
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

    It 'Rejects multipart/mixed parts without Content-Disposition' {
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

        $statusCode = $null
        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/mixed" -ContentType "multipart/mixed; boundary=$boundary" -Body $body -ErrorAction Stop | Out-Null
        } catch {
            $statusCode = $_.Exception.Response.StatusCode.value__
        }

        $statusCode | Should -Be 400
    }

    It 'Rejects multipart/mixed without boundary parameter' {
        $boundary = 'mixed-boundary'
        $body = @(
            "--$boundary",
            'Content-Disposition: form-data; name="text"',
            'Content-Type: text/plain',
            '',
            'first',
            "--$boundary--",
            ''
        ) -join "`r`n"

        $statusCode = $null
        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/mixed" -ContentType 'multipart/mixed' -Body $body -ErrorAction Stop | Out-Null
        } catch {
            $statusCode = $_.Exception.Response.StatusCode.value__
        }

        $statusCode | Should -Be 400
    }
}
