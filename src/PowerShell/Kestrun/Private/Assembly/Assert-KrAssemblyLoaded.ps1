<#
    .SYNOPSIS
        Ensures that a .NET assembly is loaded only once.

    .DESCRIPTION
        Checks the currently loaded assemblies for the specified path. If the
        assembly has not been loaded yet, it is added to the current AppDomain.
    .PARAMETER AssemblyPath
        Path to the assembly file to load.
    .PARAMETER LoadContext
        The AssemblyLoadContext to load the assembly into. Default is the Default context.
        This helps avoid issues with multiple contexts loading the same assembly.
    .OUTPUTS
        [bool] - True if the assembly is loaded successfully or already loaded, false otherwise.
#>
function Assert-KrAssemblyLoaded {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$AssemblyPath,

        # Default = require it to be loadable from the Default ALC (fixes VSCode/PSES split-load issue)
        [System.Runtime.Loader.AssemblyLoadContext]$LoadContext = [System.Runtime.Loader.AssemblyLoadContext]::Default
    )

    if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
        throw "Assembly not found at path: $AssemblyPath"
    }

    $full = (Resolve-Path -LiteralPath $AssemblyPath).Path
    $asmName = [System.Reflection.AssemblyName]::GetAssemblyName($full)

    # Find an already-loaded assembly with same simple name *in the desired ALC*
    $loadedInContext = [AppDomain]::CurrentDomain.GetAssemblies() |
        Where-Object {
            $_.GetName().Name -eq $asmName.Name -and
            ([System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext($_) -eq $LoadContext)
        } |
        Select-Object -First 1

    if ($loadedInContext) {
        Write-Verbose ('Assembly already loaded in {0} ALC: {1} {2} from {3}' -f `
            ($LoadContext.Name ?? 'Default'), $loadedInContext.GetName().Name, $loadedInContext.GetName().Version, $loadedInContext.Location)
        return $true
    }

    Write-Verbose ('Loading assembly into {0} ALC: {1}' -f ($LoadContext.Name ?? 'Default'), $full)

    try {
        # Load into the requested context (Default by default)
        [void]$LoadContext.LoadFromAssemblyPath($full)
        return $true
    } catch {
        Write-Error "Failed to load assembly: $full"
        Write-Error $_
        return $false
    }
}
