<#
    .SYNOPSIS
        Ensures that a .NET assembly is loaded only once.

    .DESCRIPTION
        Checks the currently loaded assemblies for the specified path. If the
        assembly has not been loaded yet, it is added to the current AppDomain.
    .PARAMETER AssemblyPath
        Path to the assembly file to load.
#>
function Assert-KrAssemblyLoaded {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    [CmdletBinding()]
    [OutputType([bool])]
    param (
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath
    )
    if (-not (Test-Path -Path $AssemblyPath -PathType Leaf)) {
        throw "Assembly not found at path: $AssemblyPath"
    }
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($AssemblyPath).Name
    $loaded = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $assemblyName }
    if (-not $loaded) {
        if ($Verbose) {
            Write-Verbose "Loading assembly: $AssemblyPath"
        }
        try {
            Add-Type -LiteralPath $AssemblyPath
        } catch {
            Write-Error "Failed to load assembly: $AssemblyPath"
            return $false
        }
    } else {
        if ($Verbose) {
            Write-Verbose "Assembly already loaded: $AssemblyPath"
        }
    }
    return $true
}
