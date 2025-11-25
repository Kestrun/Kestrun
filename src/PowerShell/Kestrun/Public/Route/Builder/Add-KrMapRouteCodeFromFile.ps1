<#
.SYNOPSIS
    Adds code from a file to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteCodeFromFile cmdlet adds code from a specified file to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the code will be added.
.PARAMETER CodeFilePath
    The file path to the code file that defines the route's behavior.
.PARAMETER ExtraImports
    (Optional) An array of additional namespaces to import for the script.
.PARAMETER ExtraRefs
    (Optional) An array of additional assemblies to reference for the script.
.PARAMETER Arguments
    (Optional) A hashtable of arguments to pass to the script.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteCodeFromFile -MapRouteBuilder $mapRouteBuilder -CodeFilePath 'C:\Scripts\MyRouteScript.ps1' `
    -ExtraImports @('System.IO') -ExtraRefs @([System.IO]) -Arguments @{ param1 = 'value1' }
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteCodeFromFile {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter(Mandatory = $true)]
        [string]$CodeFilePath,
        [string[]]$ExtraImports = $null,
        [System.Reflection.Assembly[]]$ExtraRefs = $null,
        [hashtable]$Arguments
    )
    process {
        return $MapRouteBuilder.AddCodeFromFile(
            $CodeFilePath,
            $ExtraImports,
            $ExtraRefs,
            $Arguments)
    }
}
