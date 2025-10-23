<#
.SYNOPSIS
    Adds a script block to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteScriptBlock cmdlet adds a PowerShell script block to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the script block will be added.
.PARAMETER ScriptBlock
    The PowerShell script block that defines the route's behavior.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteScriptBlock -MapRouteBuilder $mapRouteBuilder -ScriptBlock {
        Write-KrLog -Level Debug -Message 'Handling request'
        Write-KrJsonResponse -InputObject @{ message = 'Hello, World!' }
    }
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteScriptBlock {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter(Mandatory = $true)]
        [scriptblock]$ScriptBlock

    )
    process {
        $MapRouteBuilder.ScriptCode.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
        $MapRouteBuilder.ScriptCode.Code = $ScriptBlock.ToString()

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
