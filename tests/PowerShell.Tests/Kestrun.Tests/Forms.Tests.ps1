<#
.SYNOPSIS
    Pester tests for Kestrun Form Parsing functionality.

.DESCRIPTION
    Tests all form parsing modes:
    - multipart/form-data (file uploads)
    - application/x-www-form-urlencoded
    - multipart/mixed (ordered parts)
    - Nested multipart
    - Request-level gzip compression
    - Part-level decompression
#>

BeforeAll {
    # Import Pester helpers
    . "$PSScriptRoot/PesterHelpers.ps1"

    # Get Kestrun module path
    $kestrunModulePath = Get-KestrunModulePath
    Import-Module $kestrunModulePath -Force -ErrorAction Stop

    # Helper to create multipart body with boundary
    function New-MultipartBody {
        param(
            [string]$Boundary,
            [hashtable[]]$Parts
        )

        $body = ''
        foreach ($part in $Parts) {
            $body += "--$Boundary`r`n"

            if ($part.Headers) {
                foreach ($header in $part.Headers.GetEnumerator()) {
                    $body += "$($header.Key): $($header.Value)`r`n"
                }
            }

            $body += "`r`n$($part.Content)`r`n"
        }
        $body += "--$Boundary--`r`n"

        return $body
    }

    # Helper to gzip compress data
    function Compress-Gzip {
        param([string]$Data)

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Data)
        $ms = [System.IO.MemoryStream]::new()
        $gzip = [System.IO.Compression.GzipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress)
        $gzip.Write($bytes, 0, $bytes.Length)
        $gzip.Close()
        $compressed = $ms.ToArray()
        $ms.Dispose()

        return $compressed
    }
}

Describe 'Form Parsing - multipart/form-data' {
    BeforeAll {
        $testScript = {
            Import-Module './src/PowerShell/Kestrun/Kestrun.psm1' -Force

            $uploadDir = Join-Path $PSScriptRoot 'test_uploads'
            New-Item -ItemType Directory -Path $uploadDir -Force | Out-Null

            New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

            $server = New-KrServer -Name 'FormTest'
            $server | Add-KrEndpoint -Port 5000 -Protocol Http

            $options = [Kestrun.Forms.KrFormOptions]::new()
            $options.DefaultUploadPath = $uploadDir
            $options.ComputeSha256 = $true

            $server | Add-KrFormRoute -Pattern '/upload' -Options $options -Scriptblock {
                param($Form)
                $payload = $Form.Payload

                $result = @{
                    fieldCount = $payload.Fields.Count
                    fileFieldCount = $payload.Files.Count
                    fields = @{}
                    files = @()
                }

                foreach ($kvp in $payload.Fields.GetEnumerator()) {
                    $result.fields[$kvp.Key] = $kvp.Value
                }

                foreach ($kvp in $payload.Files.GetEnumerator()) {
                    foreach ($file in $kvp.Value) {
                        $result.files += @{
                            fieldName = $kvp.Key
                            originalFileName = $file.OriginalFileName
                            size = $file.Length
                            sha256 = $file.Sha256
                        }
                    }
                }

                Write-KrJsonResponse $result
            }

            $server | Enable-KrConfiguration | Start-KrServer
        }

        $instance = Start-ExampleScript -Scriptblock $testScript
        $baseUrl = "http://localhost:$($instance.Port)"
    }

    AfterAll {
        if ($instance) {
            Stop-ExampleScript -Instance $instance
        }
    }

    It 'Should parse multipart/form-data with files and fields' {
        $multipart = [System.Net.Http.MultipartFormDataContent]::new()
        $multipart.Add([System.Net.Http.StringContent]::new('testuser'), 'username')
        $multipart.Add([System.Net.Http.StringContent]::new('Test description'), 'description')

        $file1Content = [System.Text.Encoding]::UTF8.GetBytes('Test file content 1')
        $content1 = [System.Net.Http.ByteArrayContent]::new($file1Content)
        $multipart.Add($content1, 'files', 'test1.txt')

        $file2Content = [System.Text.Encoding]::UTF8.GetBytes('Test file content 2')
        $content2 = [System.Net.Http.ByteArrayContent]::new($file2Content)
        $multipart.Add($content2, 'files', 'test2.txt')

        $client = [System.Net.Http.HttpClient]::new()
        $response = $client.PostAsync("$baseUrl/upload", $multipart).Result
        $json = $response.Content.ReadAsStringAsync().Result | ConvertFrom-Json

        $json.fieldCount | Should -Be 2
        $json.fileFieldCount | Should -Be 1
        $json.fields.username | Should -Contain 'testuser'
        $json.fields.description | Should -Contain 'Test description'
        $json.files.Count | Should -Be 2
        $json.files[0].originalFileName | Should -Be 'test1.txt'
        $json.files[1].originalFileName | Should -Be 'test2.txt'
        $json.files[0].sha256 | Should -Not -BeNullOrEmpty

        $client.Dispose()
    }
}

Describe 'Form Parsing - application/x-www-form-urlencoded' {
    BeforeAll {
        $testScript = {
            Import-Module './src/PowerShell/Kestrun/Kestrun.psm1' -Force

            New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

            $server = New-KrServer -Name 'FormTest'
            $server | Add-KrEndpoint -Port 5000 -Protocol Http

            $server | Add-KrFormRoute -Pattern '/form' -Scriptblock {
                param($Form)
                $payload = $Form.Payload

                $result = @{
                    fields = @{}
                }

                foreach ($kvp in $payload.Fields.GetEnumerator()) {
                    $result.fields[$kvp.Key] = $kvp.Value
                }

                Write-KrJsonResponse $result
            }

            $server | Enable-KrConfiguration | Start-KrServer
        }

        $instance = Start-ExampleScript -Scriptblock $testScript
        $baseUrl = "http://localhost:$($instance.Port)"
    }

    AfterAll {
        if ($instance) {
            Stop-ExampleScript -Instance $instance
        }
    }

    It 'Should parse URL-encoded form data' {
        $body = @{
            username = 'john'
            email = 'john@example.com'
            message = 'Hello world'
        }

        $response = Invoke-RestMethod -Uri "$baseUrl/form" -Method Post -Body $body

        $response.fields.username | Should -Contain 'john'
        $response.fields.email | Should -Contain 'john@example.com'
        $response.fields.message | Should -Contain 'Hello world'
    }
}

Describe 'Form Parsing - multipart/mixed Ordered Parts' {
    BeforeAll {
        $testScript = {
            Import-Module './src/PowerShell/Kestrun/Kestrun.psm1' -Force

            $tempDir = Join-Path $PSScriptRoot 'test_temp'
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

            New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

            $server = New-KrServer -Name 'FormTest'
            $server | Add-KrEndpoint -Port 5000 -Protocol Http

            $options = [Kestrun.Forms.KrFormOptions]::new()
            $options.DefaultUploadPath = $tempDir

            $server | Add-KrFormRoute -Pattern '/mixed' -Options $options -Scriptblock {
                param($Form)
                $payload = $Form.Payload

                $result = @{
                    partCount = $payload.Parts.Count
                    parts = @()
                }

                foreach ($part in $payload.Parts) {
                    $result.parts += @{
                        contentType = $part.ContentType
                        size = $part.Length
                    }
                }

                Write-KrJsonResponse $result
            }

            $server | Enable-KrConfiguration | Start-KrServer
        }

        $instance = Start-ExampleScript -Scriptblock $testScript
        $baseUrl = "http://localhost:$($instance.Port)"
    }

    AfterAll {
        if ($instance) {
            Stop-ExampleScript -Instance $instance
        }
    }

    It 'Should parse multipart/mixed with ordered parts' {
        $boundary = 'boundary-' + (Get-Random)
        $body = New-MultipartBody -Boundary $boundary -Parts @(
            @{
                Headers = @{ 'Content-Type' = 'application/json' }
                Content = '{"id":1}'
            },
            @{
                Headers = @{ 'Content-Type' = 'text/plain' }
                Content = 'Plain text part'
            },
            @{
                Headers = @{ 'Content-Type' = 'text/html' }
                Content = '<p>HTML part</p>'
            }
        )

        $headers = @{
            'Content-Type' = "multipart/mixed; boundary=$boundary"
        }

        $response = Invoke-RestMethod -Uri "$baseUrl/mixed" -Method Post -Body $body -Headers $headers

        $response.partCount | Should -Be 3
        $response.parts[0].contentType | Should -Be 'application/json'
        $response.parts[1].contentType | Should -Be 'text/plain'
        $response.parts[2].contentType | Should -Be 'text/html'
    }
}

Describe 'Form Parsing - Nested Multipart' {
    BeforeAll {
        $testScript = {
            Import-Module './src/PowerShell/Kestrun/Kestrun.psm1' -Force

            $tempDir = Join-Path $PSScriptRoot 'test_temp'
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

            New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

            $server = New-KrServer -Name 'FormTest'
            $server | Add-KrEndpoint -Port 5000 -Protocol Http

            $options = [Kestrun.Forms.KrFormOptions]::new()
            $options.DefaultUploadPath = $tempDir
            $options.Limits.MaxNestingDepth = 1

            $server | Add-KrFormRoute -Pattern '/nested' -Options $options -Scriptblock {
                param($Form)
                $payload = $Form.Payload

                $result = @{
                    partCount = $payload.Parts.Count
                    nestedCount = 0
                    nestedPartCount = 0
                }

                foreach ($part in $payload.Parts) {
                    if ($part.NestedPayload) {
                        $result.nestedCount++
                        if ($part.NestedPayload -is [Kestrun.Forms.KrOrderedPartsPayload]) {
                            $result.nestedPartCount = $part.NestedPayload.Parts.Count
                        }
                    }
                }

                Write-KrJsonResponse $result
            }

            $server | Enable-KrConfiguration | Start-KrServer
        }

        $instance = Start-ExampleScript -Scriptblock $testScript
        $baseUrl = "http://localhost:$($instance.Port)"
    }

    AfterAll {
        if ($instance) {
            Stop-ExampleScript -Instance $instance
        }
    }

    It 'Should parse nested multipart sections' {
        $innerBoundary = 'inner-' + (Get-Random)
        $outerBoundary = 'outer-' + (Get-Random)

        # Build nested multipart
        $nestedBody = New-MultipartBody -Boundary $innerBoundary -Parts @(
            @{
                Headers = @{ 'Content-Type' = 'text/plain' }
                Content = 'Nested part 1'
            },
            @{
                Headers = @{ 'Content-Type' = 'application/json' }
                Content = '{"nested":true}'
            }
        )

        # Build outer multipart with nested section
        $body = New-MultipartBody -Boundary $outerBoundary -Parts @(
            @{
                Headers = @{ 'Content-Type' = 'application/json' }
                Content = '{"type":"metadata"}'
            },
            @{
                Headers = @{ 'Content-Type' = "multipart/mixed; boundary=$innerBoundary" }
                Content = $nestedBody.TrimEnd()
            },
            @{
                Headers = @{ 'Content-Type' = 'text/plain' }
                Content = 'Final part'
            }
        )

        $headers = @{
            'Content-Type' = "multipart/mixed; boundary=$outerBoundary"
        }

        $response = Invoke-RestMethod -Uri "$baseUrl/nested" -Method Post -Body $body -Headers $headers

        $response.partCount | Should -Be 3
        $response.nestedCount | Should -Be 1
        $response.nestedPartCount | Should -Be 2
    }
}

Describe 'Form Parsing - Request-Level Gzip' {
    Context 'With RequestDecompression middleware enabled' {
        BeforeAll {
            $testScript = {
                Import-Module './src/PowerShell/Kestrun/Kestrun.psm1' -Force

                New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

                $server = New-KrServer -Name 'FormTest'
                $server | Add-KrEndpoint -Port 5000 -Protocol Http

                # Enable request decompression
                $server | Add-KrRequestDecompressionMiddleware

                $server | Add-KrFormRoute -Pattern '/upload' -Scriptblock {
                    param($Form)
                    $payload = $Form.Payload

                    Write-KrJsonResponse @{
                        success = $true
                        fieldCount = $payload.Fields.Count
                    }
                }

                $server | Enable-KrConfiguration | Start-KrServer
            }

            $instance = Start-ExampleScript -Scriptblock $testScript
            $baseUrl = "http://localhost:$($instance.Port)"
        }

        AfterAll {
            if ($instance) {
                Stop-ExampleScript -Instance $instance
            }
        }

        It 'Should handle gzip-compressed request body' {
            $boundary = 'boundary-' + (Get-Random)
            $plainBody = New-MultipartBody -Boundary $boundary -Parts @(
                @{
                    Headers = @{ 'Content-Disposition' = 'form-data; name="username"' }
                    Content = 'testuser'
                }
            )

            $compressed = Compress-Gzip -Data $plainBody

            $uri = [Uri]::new("$baseUrl/upload")
            $request = [System.Net.HttpWebRequest]::Create($uri)
            $request.Method = 'POST'
            $request.ContentType = "multipart/form-data; boundary=$boundary"
            $request.Headers.Add('Content-Encoding', 'gzip')
            $request.ContentLength = $compressed.Length

            $stream = $request.GetRequestStream()
            $stream.Write($compressed, 0, $compressed.Length)
            $stream.Close()

            $response = $request.GetResponse()
            $reader = [System.IO.StreamReader]::new($response.GetResponseStream())
            $json = $reader.ReadToEnd() | ConvertFrom-Json
            $reader.Close()
            $response.Close()

            $json.success | Should -Be $true
            $json.fieldCount | Should -Be 1
        }
    }

    Context 'Without RequestDecompression middleware' {
        BeforeAll {
            $testScript = {
                Import-Module './src/PowerShell/Kestrun/Kestrun.psm1' -Force

                New-KrLogger | Set-KrLoggerLevel -Value Debug | Add-KrSinkConsole | Register-KrLogger -SetAsDefault

                $server = New-KrServer -Name 'FormTest'
                $server | Add-KrEndpoint -Port 5000 -Protocol Http

                # No request decompression middleware

                $server | Add-KrFormRoute -Pattern '/upload' -Scriptblock {
                    param($Form)
                    Write-KrJsonResponse @{ success = $true }
                }

                $server | Enable-KrConfiguration | Start-KrServer
            }

            $instance = Start-ExampleScript -Scriptblock $testScript
            $baseUrl = "http://localhost:$($instance.Port)"
        }

        AfterAll {
            if ($instance) {
                Stop-ExampleScript -Instance $instance
            }
        }

        It 'Should fail to parse gzip-compressed request without middleware' {
            $boundary = 'boundary-' + (Get-Random)
            $plainBody = New-MultipartBody -Boundary $boundary -Parts @(
                @{
                    Headers = @{ 'Content-Disposition' = 'form-data; name="username"' }
                    Content = 'testuser'
                }
            )

            $compressed = Compress-Gzip -Data $plainBody

            $uri = [Uri]::new("$baseUrl/upload")
            $request = [System.Net.HttpWebRequest]::Create($uri)
            $request.Method = 'POST'
            $request.ContentType = "multipart/form-data; boundary=$boundary"
            $request.Headers.Add('Content-Encoding', 'gzip')
            $request.ContentLength = $compressed.Length

            $stream = $request.GetRequestStream()
            $stream.Write($compressed, 0, $compressed.Length)
            $stream.Close()

            # Should fail because compressed data can't be parsed as multipart
            { $request.GetResponse() } | Should -Throw
        }
    }
}
