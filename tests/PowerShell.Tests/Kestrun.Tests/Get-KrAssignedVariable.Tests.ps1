Describe 'Get-KrAssignedVariable' {
    BeforeAll {
        . "$PSScriptRoot/../../../src/PowerShell/Kestrun/Private/Variable/Get-KrAssignedVariables.ps1"
    }

    It 'captures typed declaration-only variables (no assignment)' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            [int]$declOnly
            [int]$assigned = 42

            $museumHoursValue = @(
                [ordered]@{ date = '2023-09-11'; timeOpen = '09:00'; timeClose = '18:00' }
                [ordered]@{ date = '2023-09-12'; timeOpen = '09:00'; timeClose = '18:00' }
            )
        }

        ($vars | Where-Object Name -eq 'declOnly').Count | Should -Be 1
        ($vars | Where-Object Name -eq 'declOnly').Type | Should -Be 'int'

        ($vars | Where-Object Name -eq 'assigned').Count | Should -Be 1
        ($vars | Where-Object Name -eq 'assigned').Value | Should -Be 42

        ($vars | Where-Object Name -eq 'museumHoursValue').Count | Should -Be 1
        (($vars | Where-Object Name -eq 'museumHoursValue').Value.Count) | Should -Be 2
    }
}
