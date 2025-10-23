<#
.SYNOPSIS
    Adds a code block to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteCodeBlock cmdlet adds a code block in a specified scripting language to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the code block will be added.
.PARAMETER CodeBlock
    The code block that defines the route's behavior.
.PARAMETER Language
    The scripting language of the code block (e.g., PowerShell, CSharp).
.PARAMETER ExtraImports
    (Optional) An array of additional namespaces to import for the script.
.PARAMETER ExtraRefs
    (Optional) An array of additional assemblies to reference for the script.
.PARAMETER Arguments
    (Optional) A hashtable of arguments to pass to the script.
.PARAMETER LanguageVersion
    (Optional) The language version for the script. Default is 'Latest'.
.EXAMPLE
    # Create a new Map Route Builder
   $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteCodeBlock -MapRouteBuilder $mapRouteBuilder -CodeBlock {
        Console.WriteLine("Hello from C# code block!");
    } -Language 'CSharp' -ExtraImports @('System.Linq') -ExtraRefs @([System.Core]) -Arguments @{ param1 = 'value1'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteCodeBlock {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter(Mandatory = $true)]
        [string]$CodeBlock,
        [Parameter(Mandatory = $true)]
        [Kestrun.Scripting.ScriptLanguage]$Language,
        [string[]]$ExtraImports = $null,
        [System.Reflection.Assembly[]]$ExtraRefs = $null,
        [hashtable]$Arguments,
        [Microsoft.CodeAnalysis.CSharp.LanguageVersion]$LanguageVersion = 'Latest'
    )
    process {
        $MapRouteBuilder.ScriptCode.Language = $Language
        $MapRouteBuilder.ScriptCode.ExtraImports = $ExtraImports
        $MapRouteBuilder.ScriptCode.ExtraRefs = $ExtraRefs
        $MapRouteBuilder.ScriptCode.Code = $CodeBlock
        $MapRouteBuilder.ScriptCode.LanguageVersion = $LanguageVersion

        if ($null -ne $Arguments) {
            $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
            foreach ($key in $Arguments.Keys) {
                $dict[$key] = $Arguments[$key]
            }
            $MapRouteBuilder.ScriptCode.Arguments = $dict
        }

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
