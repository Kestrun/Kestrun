param()

Describe 'Example 10.2-Compression' -Tag 'Tutorial', 'Middleware', 'Compression' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '10.2-Compression.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'All main routes respond 200 without compression header' {
        foreach ($p in '/text', '/json', '/html', '/xml', '/form', '/info', '/raw-nocompress') {
            Invoke-ExampleRequest -Uri "$( $script:instance.Url)$p" | Out-Null
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
        $noEnc = Invoke-WebRequest -Uri "$base/info" -UseBasicParsing -TimeoutSec 10 -SkipCertificateCheck
        $withEnc = Invoke-WebRequest -Uri "$base/info" -UseBasicParsing -TimeoutSec 10 -SkipCertificateCheck -Headers @{ 'Accept-Encoding' = 'gzip' }

        $noEnc.StatusCode | Should -Be 200
        $withEnc.StatusCode | Should -Be 200

        $withEnc.Content | Should -Match 'AcceptEncoding'
        if ($withEnc.Headers['Content-Encoding']) { $withEnc.Headers['Content-Encoding'] | Should -Be 'gzip' }
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

Describe 'Example 10.2-Compression Providers' -Tag 'Tutorial', 'Middleware', 'Compression', 'Providers' {
    Context 'Gzip only (Brotli disabled)' {
        BeforeAll {
            . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
            $script:gzipOnly = Start-ExampleScript -Scriptblock {
                param([int]$Port)
                New-KrServer -Name 'GzipOnly'
                Add-KrEndpoint -Port $Port -SelfSignedCert | Out-Null
                Add-KrPowerShellRuntime
                Add-KrCompressionMiddleware -EnableForHttps -DisableBrotli -MimeTypes 'text/plain', 'application/json'
                Enable-KrConfiguration
                function _NewLargeBlock([string]$seed, [int]$r = 60) { (($seed + ' ') * $r).Trim() }
                Add-KrMapRoute -Verbs Get -Pattern '/gztxt' -Scriptblock {
                    $body = _NewLargeBlock 'GzipOnly Payload Text' 100
                    Write-KrTextResponse -InputObject $body -StatusCode 200
                }
                Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
                        Pattern = '/gztxt-nocompress'
                        HttpVerbs = 'Get'
                        Code = {
                            $body = _NewLargeBlock 'GzipOnly NoCompress' 100
                            Write-KrTextResponse -InputObject $body -StatusCode 200
                        }
                        Language = 'PowerShell'
                        DisableResponseCompression = $true
                    })
                Start-KrServer
            }
        }
        AfterAll { if ($script:gzipOnly) { Stop-ExampleScript -Instance $script:gzipOnly } }

        It 'Returns gzip content-encoding when requested (no br advertised)' {
            $probe = Get-CompressionProbe -Instance $script:gzipOnly -Path '/gztxt'
            $probe.GzipEncoding | Should -Be 'gzip'
            ($probe.GzipLength -lt $probe.RawLength) | Should -BeTrue
        }
        It 'Opt-out route remains uncompressed under gzip-only mode' {
            $probe = Get-CompressionProbe -Instance $script:gzipOnly -Path '/gztxt-nocompress'
            $probe.GzipEncoding | Should -BeNullOrEmpty
        }
        It 'Brotli-only request falls back (no br provider available)' {
            $base = $script:gzipOnly.Url
            $resp = Invoke-WebRequest -Uri "$base/gztxt" -UseBasicParsing -Headers @{ 'Accept-Encoding' = 'br' } -SkipCertificateCheck -TimeoutSec 12
            # Expect either no Content-Encoding (uncompressed fallback) or still gzip if runtime ignores unsupported request list ordering
            $ce = $resp.Headers['Content-Encoding']
            if ($ce) {
                $ce | Should -Be 'gzip'
            } else {
                # ensure body present and plausible size ( > 1000 bytes )
                ($resp.RawContentLength -gt 1000) | Should -BeTrue -Because 'Uncompressed fallback should return original payload'
            }
        }
    }

    Context 'Brotli only (Gzip disabled)' {
        BeforeAll {
            . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
            $script:brOnly = Start-ExampleScript -Scriptblock {
                param([int]$Port)
                New-KrServer -Name 'BrotliOnly'
                Add-KrEndpoint -Port $Port -SelfSignedCert | Out-Null
                Add-KrPowerShellRuntime
                Add-KrCompressionMiddleware -EnableForHttps -DisableGzip -MimeTypes 'text/plain', 'application/json'
                Enable-KrConfiguration
                function _NewLargeBlock([string]$seed, [int]$r = 60) { (($seed + ' ') * $r).Trim() }
                Add-KrMapRoute -Verbs Get -Pattern '/brtxt' -Scriptblock {
                    $body = _NewLargeBlock 'BrotliOnly Payload Text' 120
                    Write-KrTextResponse -InputObject $body -StatusCode 200
                }
                Add-KrMapRoute -Options (New-KrMapRouteOption -Property @{
                        Pattern = '/brtxt-nocompress'
                        HttpVerbs = 'Get'
                        Code = {
                            $body = _NewLargeBlock 'BrotliOnly NoCompress' 120
                            Write-KrTextResponse -InputObject $body -StatusCode 200
                        }
                        Language = 'PowerShell'
                        DisableResponseCompression = $true
                    })
                Start-KrServer
            }
        }
        AfterAll { if ($script:brOnly) { Stop-ExampleScript -Instance $script:brOnly } }

        It 'Returns brotli content-encoding when requested (gzip disabled)' {
            $base = $script:brOnly.Url
            $resp = Invoke-WebRequest -Uri "$base/brtxt" -UseBasicParsing -Headers @{ 'Accept-Encoding' = 'br,gzip' } -SkipCertificateCheck -TimeoutSec 12
            $resp.Headers['Content-Encoding'] | Should -Be 'br'
            $raw = Invoke-WebRequest -Uri "$base/brtxt" -UseBasicParsing -SkipCertificateCheck -TimeoutSec 12
            ($resp.RawContentLength -lt $raw.RawContentLength) | Should -BeTrue
        }
        It 'Opt-out route remains uncompressed under brotli-only mode' {
            $base = $script:brOnly.Url
            $resp = Invoke-WebRequest -Uri "$base/brtxt-nocompress" -UseBasicParsing -Headers @{ 'Accept-Encoding' = 'br,gzip' } -SkipCertificateCheck -TimeoutSec 12
            $resp.Headers['Content-Encoding'] | Should -BeNullOrEmpty
        }
    }
}
