param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}


Describe 'Example 22.12 nested multipart/mixed using OpenAPI' -Tag 'Tutorial', 'multipart/form', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '22.XX-Nested-Multipart-OpenAPI.ps1'
    }
    AfterAll {
        if ($script:instance) {
            $uploadDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $script:instance.BaseName
            if (Test-Path $uploadDir) { Remove-Item -Recurse -Force $uploadDir }
            Stop-ExampleScript -Instance $script:instance
        }
    }

    # --- Success case ---------------------------------------------------------
    It 'Parses nested multipart payloads' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $outerBody = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions

        $resp = Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $outerBody
        $resp.outerCount | Should -Be 2
        $resp.nested[0].nestedCount | Should -Be 2
    }

    # --- Common failure cases -------------------------------------------------

    It 'Returns 415 when Content-Type is multipart/form-data (wrong top-level media type)' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/form-data; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 415
        }
    }

    It 'Returns 415 when Content-Type is application/json (not multipart)' {
        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType 'application/json' -Body '{"stage":"outer"}'
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 415
        }
    }

    It 'Returns 400 when multipart/mixed boundary is missing' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType 'multipart/mixed' -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'Returns 400 when outer part is missing Content-Disposition name' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions
        # (outer disposition omitted)

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'Returns 400 when nested container part is missing Content-Disposition name' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeInnerDispositions
        # (nested disposition omitted)

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'Returns 400 when outer part name is unknown (no matching rule)' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions `
            -OuterName 'outerX'  # not allowed by rules

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'Returns 400 when nested part name is unknown (no matching rule)' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions `
            -NestedName 'nestedX' # not allowed by rules

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'Returns 400 when inner parts are missing Content-Disposition names (rules apply everywhere)' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition
        # (inner dispositions omitted)

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'Returns 400 when nested content-type is not multipart/mixed (nested must be multipart per your parser)' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions `
            -NestedContentTypeHeader 'Content-Type: application/json'

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 400
        }
    }

    It 'Returns 400 when outer JSON exceeds MaxBytes (outer rule limit)' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'

        # Make outer JSON > 1024 bytes
        $big = ('a' * 1100)
        $outerJson = "{""stage"":""$big""}"

        $body = New-NestedMultipartBody `
            -OuterBoundary $outer `
            -InnerBoundary $inner `
            -IncludeOuterDisposition `
            -IncludeNestedDisposition `
            -IncludeInnerDispositions `
            -OuterJson $outerJson

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $body
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 413
        }
    }

    It 'Returns 400 when nesting depth exceeds MaxNestingDepth (1)' {
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $deep = 'deep-boundary'

        $deepBody = @(
            "--$deep",
            'Content-Disposition: form-data; name="deepText"',
            'Content-Type: text/plain',
            '',
            'too-deep',
            "--$deep--",
            ''
        ) -join "`r`n"

        # inner multipart includes another multipart => depth 2
        $innerBody = @(
            "--$inner",
            'Content-Disposition: form-data; name="text"',
            "Content-Type: multipart/mixed; boundary=$deep",
            '',
            $deepBody,
            "--$inner--",
            ''
        ) -join "`r`n"

        $outerBody = @(
            "--$outer",
            'Content-Disposition: form-data; name="outer"',
            'Content-Type: application/json',
            '',
            '{"stage":"outer"}',

            "--$outer",
            'Content-Disposition: form-data; name="nested"',
            "Content-Type: multipart/mixed; boundary=$inner",
            '',
            $innerBody,

            "--$outer--",
            ''
        ) -join "`r`n"

        try {
            Invoke-RestMethod -Method Post -Uri "$($script:instance.Url)/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $outerBody
        } catch {
            $_.Exception.Response.StatusCode.value__ | Should -Be 413
        }
    }
}
