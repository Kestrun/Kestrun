param()

Describe 'Example 22.7 part-level compression' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '22.7-part-compressed.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path (Split-Path -Parent $script:instance.TempPath) 'uploads'
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
            Stop-ExampleScript -Instance $script:instance
        }
    }

    It 'Parses gzip-compressed part when enabled' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $raw = [System.Text.Encoding]::UTF8.GetBytes('compressed-part')
        $compressed = New-GzipBinaryData -data $raw

        $part = [System.Net.Http.ByteArrayContent]::new($compressed)
        $part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $part.Headers.ContentEncoding.Add('gzip')
        $content.Add($part, 'file', 'payload.txt')

        $resp = $client.PostAsync("$($script:instance.Url)/part-compressed", $content).Result
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content.ReadAsStringAsync().Result | ConvertFrom-Json
        [int]$json.length | Should -BeGreaterThan 0
    }

    It 'Rejects parts exceeding decompressed limit' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $raw = [System.Text.Encoding]::UTF8.GetBytes(('a' * (2 * 1024 * 1024)))
        $compressed = New-GzipBinaryData -data $raw

        $part = [System.Net.Http.ByteArrayContent]::new($compressed)
        $part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $part.Headers.ContentEncoding.Add('gzip')
        $content.Add($part, 'file', 'too-big.txt')

        $resp = $client.PostAsync("$($script:instance.Url)/part-compressed", $content).Result
        $resp.StatusCode | Should -Not -Be 200
    }
}
