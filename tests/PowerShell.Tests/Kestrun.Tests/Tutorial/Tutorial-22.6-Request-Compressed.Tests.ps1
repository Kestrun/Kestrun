param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Example 22.6 request-level compression' {
    BeforeAll {
        $script:withMiddleware = Start-ExampleScript -Name '22-file-and-form-uploads/22.6-request-compressed.ps1'
        $script:withoutMiddleware = Start-ExampleScript -Name '22-file-and-form-uploads/22.1-basic-multipart.ps1'
    }
    AfterAll {
        foreach ($instance in @($script:withMiddleware, $script:withoutMiddleware)) {
            if ($instance) {
                $uploadDir = Join-Path (Split-Path -Parent $instance.TempPath) 'uploads'
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
