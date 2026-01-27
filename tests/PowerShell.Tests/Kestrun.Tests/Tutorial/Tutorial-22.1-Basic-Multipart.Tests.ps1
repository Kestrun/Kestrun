param()

Describe 'Example 22.1 Basic multipart/form-data' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '22.1-Basic-Multipart.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path (Split-Path -Parent $script:instance.TempPath) 'uploads'
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
            Stop-ExampleScript -Instance $script:instance
        }
    }

    It 'Parses fields and one file' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Hello'), 'note')
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('sample file')
        $file = [System.Net.Http.ByteArrayContent]::new($bytes)
        $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file, 'file', 'hello.txt')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content.ReadAsStringAsync().Result | ConvertFrom-Json

        $json.fields.note[0] | Should -Be 'Hello'
        $json.files.Count | Should -Be 1
        $json.files[0].fileName | Should -Be 'hello.txt'
        [int64]$json.files[0].length | Should -BeGreaterThan 0
    }

    It 'Rejects when required file part is missing' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Hello'), 'note')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        [int]$resp.StatusCode | Should -Be 400
    }

    It 'Rejects when file Content-Type is not allowed' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Hello'), 'note')
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('sample file')
        $file = [System.Net.Http.ByteArrayContent]::new($bytes)
        $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/json')
        $content.Add($file, 'file', 'hello.txt')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        [int]$resp.StatusCode | Should -Be 415
    }

    It 'Rejects when multiple files are sent for a single-file rule' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Hello'), 'note')

        $file1 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('one'))
        $file1.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file1, 'file', 'one.txt')

        $file2 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('two'))
        $file2.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file2, 'file', 'two.txt')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        [int]$resp.StatusCode | Should -Be 400
    }
}
