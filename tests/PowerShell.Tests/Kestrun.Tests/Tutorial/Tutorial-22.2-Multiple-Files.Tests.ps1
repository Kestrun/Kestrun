param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 22.2 Multiple files under same field' -Tag 'Tutorial', 'multipart/form', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '22.2-Multiple-Files.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $script:instance.BaseName
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
            Stop-ExampleScript -Instance $script:instance
        }
    }

    It 'Accepts two files in the same field name' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Batch'), 'note')

        $file1 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('one'))
        $file1.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file1, 'files', 'one.txt')

        $file2 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('two'))
        $file2.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file2, 'files', 'two.txt')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content.ReadAsStringAsync().Result | ConvertFrom-Json

        $json.count | Should -Be 2
        ($json.files.fileName -contains 'one.txt') | Should -BeTrue
        ($json.files.fileName -contains 'two.txt') | Should -BeTrue
    }

    It 'Rejects when required files field is missing' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Batch'), 'note')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        [int]$resp.StatusCode | Should -Be 400
    }

    It 'Rejects when a file Content-Type is not allowed' {
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Batch'), 'note')

        $file = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('{"a":1}'))
        $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/json')
        $content.Add($file, 'files', 'bad.json')

        $resp = $client.PostAsync("$($script:instance.Url)/upload", $content).Result
        [int]$resp.StatusCode | Should -Be 415
    }
}
