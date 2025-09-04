
<#
    .SYNOPSIS
        Get a set of documentation for Kestrun commands.

    .DESCRIPTION
        This function retrieves a set of documentation for Kestrun commands based on the specified context.
    .PARAMETER Name
        The name of the documentation set to retrieve.
    .PARAMETER Module
        The name of the module to search for commands.
    .PARAMETER IncludeNonExported
        Whether to include non-exported commands in the search.
    .EXAMPLE
        # All Definition-only commands (pure)
        Get-KrDocSet -Name DefinitionOnly
    .EXAMPLE
        # All commands that are *only* Route
        Get-KrDocSet -Name RouteOnly
    .EXAMPLE
        # All Route+Schedule commands (pure composite)
        Get-KrDocSet -Name RouteAndSchedule
    .EXAMPLE
        # Everything usable in runtime (pure composite of Route|Schedule)
        Get-KrDocSet -Name Runtime
    .EXAMPLE
        # All config-only commands (pure Definition, no runtime use)
        Get-KrDocSet -Name ConfigOnly
#>
function Get-KrDocSet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet(
            'DefinitionOnly',
            'RouteOnly',
            'ScheduleOnly',
            'RouteAndSchedule',
            'ScheduleAndDefinition',
            'Runtime',   # Route|Schedule
            'Everywhere',
            'ConfigOnly' # Definition but not Route/Schedule
        )]
        [string]$Name,

        [string]$Module = 'Kestrun',
        [switch]$IncludeNonExported,
        [object[]]$Functions
    )

    switch ($Name) {
        'DefinitionOnly' {
            Get-KrCommandsByContext -AnyOf Definition -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
        'RouteOnly' {
            Get-KrCommandsByContext -AnyOf Route -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
        'ScheduleOnly' {
            Get-KrCommandsByContext -AnyOf Schedule -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
        'RouteAndSchedule' {
            Get-KrCommandsByContext -AllOf Route, Schedule -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
        'ScheduleAndDefinition' {
            Get-KrCommandsByContext -AnyOf ScheduleAndDefinition -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
        'Runtime' {
            Get-KrCommandsByContext -AnyOf Runtime -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
        'Everywhere' {
            Get-KrCommandsByContext -AnyOf Everywhere -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
        'ConfigOnly' {
            # Definition but NOT Route or Schedule
            Get-KrCommandsByContext -AnyOf Definition -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
        }
    }
}
