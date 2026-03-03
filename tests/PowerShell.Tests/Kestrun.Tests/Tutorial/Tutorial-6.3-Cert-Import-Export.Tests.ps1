param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 6.3-Cert-Import-Export' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '6.3-Cert-Import-Export.ps1'
        $script:tempBase = Join-Path ([System.IO.Path]::GetTempPath()) ("kestrun-cert-export-$([System.Guid]::NewGuid().ToString('N'))")
        $script:exportBase = Join-Path $script:tempBase 'devcert'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }

        if ($script:tempBase -and (Test-Path $script:tempBase)) {
            Remove-Item -Path $script:tempBase -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'POST /certs/import returns 400 for missing input file' {
        $body = @{
            filePath = (Join-Path $script:tempBase 'missing.pfx')
            password = 'p@ss'
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/certs/import" -Body $body -ContentType 'application/json' -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 400
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'

        $json = $result.Content | ConvertFrom-Json
        $json.error | Should -Not -BeNullOrEmpty
    }

    It 'POST /certs/export returns 200 and creates PFX output files' {
        if (-not (Test-Path $script:tempBase)) {
            New-Item -Path $script:tempBase -ItemType Directory -Force | Out-Null
        }

        $body = @{
            outPath = $script:exportBase
            outFormat = 'Pfx'
            includePrivateKey = $true
            password = 'p@ss'
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/certs/export" -Body $body -ContentType 'application/json' -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'

        $json = $result.Content | ConvertFrom-Json
        $json.exported | Should -Be $true
        $json.path | Should -Be $script:exportBase
        $json.format | Should -Be 'Pfx'

        (Test-Path ($script:exportBase + '.pfx')) | Should -Be $true
    }
}
