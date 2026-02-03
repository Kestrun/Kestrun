param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 22.3 Urlencoded forms' -Tag 'Tutorial', 'multipart/form', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '22.3-Url-Encoded.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $script:instance.BaseName
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }

            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Returns parsed urlencoded fields' {
        $body = 'name=Kestrun&role=admin&role=maintainer'
        $resp = Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/form" -ContentType 'application/x-www-form-urlencoded' -Body $body
        $resp.fields.name[0] | Should -Be 'Kestrun'
        $resp.fields.role.Count | Should -Be 2
    }

    It 'Rejects when required name field is missing' {
        $body = 'role=admin'
        $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/form" -ContentType 'application/x-www-form-urlencoded' -Body $body -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 400
    }
}
