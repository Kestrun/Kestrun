param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 15.2-Compression' -Tag 'Tutorial', 'Middleware', 'Compression' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '15.2-Compression.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'All main routes respond 200 without compression header' {
        foreach ($p in '/text', '/json', '/html', '/xml', '/form', '/info', '/raw-nocompress') {
            $raw = Get-HttpHeadersRaw -Uri "$( $script:instance.Url)$p" -IncludeBody -NoAcceptEncoding -Insecure:($script:instance.Url -like 'https://*')
            $raw.StatusCode | Should -Be 200
        }
    }

    It 'Gzip compression is applied to large text and json bodies' {
        $textProbe = Get-CompressionProbe -Instance $script:instance -Path '/text'
        $jsonProbe = Get-CompressionProbe -Instance $script:instance -Path '/json'

        $textProbe.GzipEncoding | Should -Be 'gzip'
        $jsonProbe.GzipEncoding | Should -Be 'gzip'

        # Ensure size benefit (allow small fluctuations; just require strictly smaller)
        ($textProbe.GzipLength -lt $textProbe.RawLength) | Should -BeTrue -Because 'Compressed text should be smaller'
        ($jsonProbe.GzipLength -lt $jsonProbe.RawLength) | Should -BeTrue -Because 'Compressed json should be smaller'
    }

    It 'Info route echoes Accept-Encoding and may or may not compress (small body tolerance)' {
        $base = $script:instance.Url
        $noEnc = Get-HttpHeadersRaw -Uri "$base/info" -IncludeBody -NoAcceptEncoding -Insecure:($base -like 'https://*')
        $gz = Get-HttpHeadersRaw -Uri "$base/info" -IncludeBody -AcceptEncoding 'gzip' -Insecure:($base -like 'https://*')
        $noEnc.StatusCode | Should -Be 200
        $gz.StatusCode | Should -Be 200
        $bodyText = Convert-BytesToStringWithGzipScan -Bytes $gz.Body
        $bodyText | Should -Match 'AcceptEncoding'
        $ce = ($gz.Headers.Keys | Where-Object { $_ -ieq 'Content-Encoding' })
        if ($ce) { $gz.Headers[$ce] | Should -Be 'gzip' } else { Write-Host 'Info route not compressed (acceptable for small body)' }
    }

    It 'XML and HTML routes compress (gzip) and shrink payload size' {
        $html = Get-CompressionProbe -Instance $script:instance -Path '/html'
        $xml = Get-CompressionProbe -Instance $script:instance -Path '/xml'
        $html.GzipEncoding | Should -Be 'gzip'
        $xml.GzipEncoding | Should -Be 'gzip'
        ($html.GzipLength -lt $html.RawLength) | Should -BeTrue
        ($xml.GzipLength -lt $xml.RawLength) | Should -BeTrue
    }

    It 'Raw no-compress route explicitly has no Content-Encoding even when requested' {
        $probe = Get-CompressionProbe -Instance $script:instance -Path '/raw-nocompress'
        $probe.GzipEncoding | Should -BeNullOrEmpty -Because 'Route opted out of compression'
        # Body length should match raw (allowing tiny header differences, so just ensure not smaller)
        ($probe.GzipLength -ge $probe.RawLength) | Should -BeTrue -Because 'No compression expected so compressed request size should not shrink'
    }
}

