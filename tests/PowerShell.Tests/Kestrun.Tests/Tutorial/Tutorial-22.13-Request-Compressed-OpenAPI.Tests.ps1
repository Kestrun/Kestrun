param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 22.13 request-level compression with OpenAPI and middleware' -Tag 'Tutorial', 'multipart/form', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:withMiddleware = Start-ExampleScript -Name '22.13-Request-Compressed-OpenAPI.ps1'
        $script:withoutMiddleware = Start-ExampleScript -Name '22.8-Basic-Multipart-OpenAPI.ps1'
    }
    AfterAll {
        foreach ($instance in @($script:withMiddleware, $script:withoutMiddleware)) {
            if ($instance) {
                $uploadDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $instance.BaseName
                if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
                Stop-ExampleScript -Instance $instance
            }
        }
    }

    It 'Parses gzip request when middleware is enabled' {
        $boundary = 'req-boundary'
        $compressed = New-GzipMultipartBody -boundary $boundary
        $resp = Invoke-WebRequest -Method Post -Uri "$($script:withMiddleware.Url)/upload" -ContentType "multipart/form-data; boundary=$boundary" -Headers @{ 'Content-Encoding' = 'gzip' } -Body $compressed
        $resp.StatusCode | Should -Be 200
    }

    It 'Rejects gzip request when required note is missing' {
        $boundary = 'req-boundary'
        $body = @(
            "--$boundary",
            'Content-Disposition: form-data; name=file; filename=hello.txt',
            'Content-Type: text/plain',
            '',
            'hello',
            "--$boundary--",
            ''
        ) -join "`r`n"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $compressed = New-GzipBinaryData -data $bytes

        $resp = Invoke-WebRequest -Method Post -Uri "$($script:withMiddleware.Url)/upload" `
            -ContentType "multipart/form-data; boundary=$boundary" -Headers @{ 'Content-Encoding' = 'gzip' } -Body $compressed -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 400
    }

    It 'Rejects gzip request when file Content-Type is not allowed' {
        $boundary = 'req-boundary'
        $body = @(
            "--$boundary",
            'Content-Disposition: form-data; name=note',
            '',
            'compressed',
            "--$boundary",
            'Content-Disposition: form-data; name=file; filename=hello.json',
            'Content-Type: application/json',
            '',
            '{"a":1}',
            "--$boundary--",
            ''
        ) -join "`r`n"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $compressed = New-GzipBinaryData -data $bytes

        $resp = Invoke-WebRequest -Method Post -Uri "$($script:withMiddleware.Url)/upload" `
            -ContentType "multipart/form-data; boundary=$boundary" -Headers @{ 'Content-Encoding' = 'gzip' } -Body $compressed -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 415
    }

    It 'Rejects gzip request when multiple files are sent for a single-file rule' {
        $boundary = 'req-boundary'
        $body = @(
            "--$boundary",
            'Content-Disposition: form-data; name=note',
            '',
            'compressed',
            "--$boundary",
            'Content-Disposition: form-data; name=file; filename=one.txt',
            'Content-Type: text/plain',
            '',
            'one',
            "--$boundary",
            'Content-Disposition: form-data; name=file; filename=two.txt',
            'Content-Type: text/plain',
            '',
            'two',
            "--$boundary--",
            ''
        ) -join "`r`n"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $compressed = New-GzipBinaryData -data $bytes

        $resp = Invoke-WebRequest -Method Post -Uri "$($script:withMiddleware.Url)/upload" `
            -ContentType "multipart/form-data; boundary=$boundary" -Headers @{ 'Content-Encoding' = 'gzip' } -Body $compressed -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 400
    }

    It 'Rejects gzip request when middleware is disabled' {
        $boundary = 'req-boundary'
        $compressed = New-GzipMultipartBody -boundary $boundary
        try {
            $resp = Invoke-WebRequest -Method Post -Uri "$($script:withoutMiddleware.Url)/upload" `
                -ContentType "multipart/form-data; boundary=$boundary" -Headers @{ 'Content-Encoding' = 'gzip' } -Body $compressed -SkipHttpErrorCheck
            $resp.StatusCode | Should -Not -Be 200
        } catch {
            $_ | Should -Not -BeNullOrEmpty
        }
    }
}
