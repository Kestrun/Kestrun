param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 21.2 - Razor Localization' -Tag 'Tutorial', 'Localization', 'Razor' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '21.2-Razor-Localization.ps1' -StartupTimeoutSeconds 40
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'renders localized Razor page in Italian' {
        $url = "$($script:instance.Url)/ui?lang=it-IT"
        Assert-RouteContent -Uri $url -Contains 'Ciao'
        Assert-RouteContent -Uri $url -Contains 'Salva'
        Assert-RouteContent -Uri $url -Contains 'Culture: it-IT'
    }

    It 'renders localized Razor page in default culture' {
        $url = "$($script:instance.Url)/ui"
        Assert-RouteContent -Uri $url -Contains 'Localized Razor Page'
        Assert-RouteContent -Uri $url -Contains 'Culture: en-US'
    }
}
