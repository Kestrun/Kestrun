param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 15.11 - Request Decompression (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '15.11-Request-Decompression.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Accepts gzip-compressed JSON request body' {
        $payloadObject = @{
            message = (('hello ' * 20000).Trim())
            count = 1
            items = 1..2000
            meta = @{ note = ('x' * 20000) }
        }
        $payload = $payloadObject | ConvertTo-Json -Compress -Depth 6
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $compressed = New-GzipBinaryData -data $bytes

        $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/api/echo" `
            -ContentType 'application/json' `
            -Headers @{ 'Content-Encoding' = 'gzip' } `
            -Body $compressed

        $resp.StatusCode | Should -Be 200

        $body = $resp.Content | ConvertFrom-Json
        $body.ok | Should -BeTrue
            $body.receivedBytes | Should -BeGreaterThan 100000
        $body.messageStartsWith | Should -Be 'hello'
        $body.receivedCount | Should -Be 1
    }

        It 'Accepts gzip-compressed 2MB text upload' {
            $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) ('kr-req-decomp-' + [System.IO.Path]::GetRandomFileName() + '.txt')
            try {
                New-TestFile -Path $tmpFile -Mode Text -SizeMB 2 -Force -Quiet

                $bytes = [System.IO.File]::ReadAllBytes($tmpFile)
                $compressed = New-GzipBinaryData -data $bytes

                $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/api/text" `
                    -ContentType 'text/plain' `
                    -Headers @{ 'Content-Encoding' = 'gzip' } `
                    -Body $compressed

                $resp.StatusCode | Should -Be 200

                $body = $resp.Content | ConvertFrom-Json
                $body.ok | Should -BeTrue
                $body.receivedBytes | Should -BeGreaterThan (2MB - 1024)
                $body.contentType | Should -Be 'text/plain'
            } finally {
                if (Test-Path $tmpFile) { Remove-Item $tmpFile -Force }
            }
        }
}
