[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingBrokenHashAlgorithms', '')]
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 22.15 File hash upload (OpenAPI)' -Tag 'Tutorial', 'multipart/form', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '22.15-File-Hash-OpenAPI.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $script:instance.BaseName
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }

            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Uploads a binary file and returns hashes' {
        $testFile = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-upload-hash-openapi.bin'
        New-TestFile -Path $testFile -Mode Binary -SizeMB 200 -Force -Quiet

        $expectedSha1 = (Get-FileHash -Algorithm SHA1 -Path $testFile).Hash
        $expectedSha256 = (Get-FileHash -Algorithm SHA256 -Path $testFile).Hash
        $expectedSha384 = (Get-FileHash -Algorithm SHA384 -Path $testFile).Hash
        $expectedSha512 = (Get-FileHash -Algorithm SHA512 -Path $testFile).Hash
        $expectedMd5 = (Get-FileHash -Algorithm MD5 -Path $testFile).Hash
        $expectedSize = (Get-Item $testFile).Length

        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $stream = [System.IO.File]::OpenRead($testFile)
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
        $content.Add($fileContent, 'file', (Split-Path $testFile -Leaf))

        $resp = $client.PostAsync("$($script:instance.Url)/upload-hash", $content).Result
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content.ReadAsStringAsync().Result | ConvertFrom-Json

        $json.fileName | Should -Be (Split-Path $testFile -Leaf)
        [int64]$json.size | Should -Be $expectedSize
        $json.sha1 | Should -Be $expectedSha1
        $json.sha256 | Should -Be $expectedSha256
        $json.sha384 | Should -Be $expectedSha384
        $json.sha512 | Should -Be $expectedSha512
        $json.md5 | Should -Be $expectedMd5

        $stream.Dispose()
        $content.Dispose()
        $client.Dispose()
        Remove-Item -Force $testFile
    }
}
