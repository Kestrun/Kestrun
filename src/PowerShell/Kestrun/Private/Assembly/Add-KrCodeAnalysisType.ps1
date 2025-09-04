<#
    .SYNOPSIS
        Adds the specified version of the Microsoft.CodeAnalysis assemblies to the session.
    .DESCRIPTION
        This function loads the specified version of the Microsoft.CodeAnalysis assemblies into the current session.
    .PARAMETER ModuleRootPath
        The root path of the module.
    .PARAMETER Version
        The version of the Microsoft.CodeAnalysis assemblies to load.
#>
function Add-KrCodeAnalysisType {
    [CmdletBinding()]
    [OutputType([bool])]
    param (
        [Parameter(Mandatory = $true)]
        [string]$ModuleRootPath,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    $passVerbose = $PSCmdlet.MyInvocation.BoundParameters['Verbose']
    $codeAnalysisassemblyLoadPath = Join-Path -Path $ModuleRootPath -ChildPath 'lib' -AdditionalChildPath 'Microsoft.CodeAnalysis', $Version
    return(
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.dll') -Verbose:$passVerbose) -and
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.Workspaces.dll') -Verbose:$passVerbose) -and
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.CSharp.dll') -Verbose:$passVerbose) -and
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.CSharp.Scripting.dll') -Verbose:$passVerbose) -and
        #Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.Razor.dll") -Verbose:$passVerbose
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.VisualBasic.dll') -Verbose:$passVerbose) -and
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll') -Verbose:$passVerbose) -and
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.CSharp.Workspaces.dll') -Verbose:$passVerbose) -and
        (Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath 'Microsoft.CodeAnalysis.Scripting.dll') -Verbose:$passVerbose)
    )
}

