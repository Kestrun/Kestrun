param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 11.1-RazorPages' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '11.1-RazorPages.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Serves the Razor home page' {
        Assert-RouteContent -Uri "$($script:instance.Url)/" -Contains 'What this demo shows'
        Assert-RouteContent -Uri "$($script:instance.Url)/" -Contains 'PowerShell prepares a model'
    }

    It 'Runs the sibling PowerShell model script for /About' {
        Assert-RouteContent -Uri "$($script:instance.Url)/About" -Contains 'Features in this sample'
        Assert-RouteContent -Uri "$($script:instance.Url)/About" -Contains 'Sibling script convention'
    }

    It 'Shows request-derived data on /Status' {
        Assert-RouteContent -Uri "$($script:instance.Url)/Status" -Contains '<strong>Method:</strong>'
        Assert-RouteContent -Uri "$($script:instance.Url)/Status" -Contains '<strong>Path:</strong> /Status'
    }
}
