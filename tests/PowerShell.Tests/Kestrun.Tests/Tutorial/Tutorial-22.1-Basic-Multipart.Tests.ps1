param()

Describe 'Example 22.1 Basic multipart/form-data' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '22-file-and-form-uploads/22.1-basic-multipart.ps1'
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
        $content.Add([System.Net.Http.StringContent]::new('Hello'),'note')
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('sample file')
        $file = [System.Net.Http.ByteArrayContent]::new($bytes)
        $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file,'file','hello.txt')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content.ReadAsStringAsync().Result | ConvertFrom-Json

        $json.fields.note[0] | Should -Be 'Hello'
        $json.files.Count | Should -Be 1
        $json.files[0].fileName | Should -Be 'hello.txt'
        [int64]$json.files[0].length | Should -BeGreaterThan 0
    }
}
