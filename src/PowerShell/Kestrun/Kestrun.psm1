param()

# Main Kestrun module path
# This is the root path for the Kestrun module
$moduleRootPath = Split-Path -Parent -Path $MyInvocation.MyCommand.Path

if (([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'Kestrun.Annotations' }).Count -eq 0) {
    Add-Type -LiteralPath (Join-Path -Path $moduleRootPath -ChildPath 'lib' -AdditionalChildPath 'assemblies', 'Kestrun.Annotations.dll')
}

# check PowerShell version
if ($PSVersionTable.PSVersion.Major -ne 7) {
    throw 'Unsupported PowerShell version. Please use PowerShell 7.4.'
}
# Check PowerShell minor version
switch ($PSVersionTable.PSVersion.Minor) {
    0 { throw 'Unsupported PowerShell version. Please use PowerShell 7.4.' }
    1 { throw 'Unsupported PowerShell version. Please use PowerShell 7.4.' }
    2 { throw 'Unsupported PowerShell version. Please use PowerShell 7.4.' }
    3 { throw 'Unsupported PowerShell version. Please use PowerShell 7.4.' }
    4 { $netVersion = 'net8.0'; $codeAnalysisVersion = '4.9.2' }
    5 { $netVersion = 'net8.0'; $codeAnalysisVersion = '4.11.0' }
    6 { $netVersion = 'net9.0'; $codeAnalysisVersion = '4.14.0' }
    default { $netVersion = 'net9.0'; $codeAnalysisVersion = '4.14.0' }
}
# Determine if this is a release distribution
if ( ([KestrunAnnotationsRuntimeInfo]::IsReleaseDistribution)) {
    # Load private functions
    . "$moduleRootPath/Private.ps1"
} else {
    # Load private functions
    Get-ChildItem -Path (Join-Path -Path $moduleRootPath -ChildPath 'Private') -Filter *.ps1 -Recurse -File |
        ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }
}
# only import public functions
$sysfuncs = Get-ChildItem Function:

# only import public alias
$sysaliases = Get-ChildItem Alias:

# Compute assembly load path ONCE so both branches see it
$assemblyLoadPath = Join-Path -Path (Join-Path -Path $moduleRootPath -ChildPath 'lib') -ChildPath $netVersion

# Determine if we are in a route runspace by checking for the KrServer variable
$inRouteRunspace = $null -ne $ExecutionContext.SessionState.PSVariable.GetValue('KrServer')

if (-not $inRouteRunspace) {
    # Usage
    if ((Add-KrAspNetCoreType -Version $netVersion ) -and
        (Add-KrCodeAnalysisType -ModuleRootPath $moduleRootPath -Version $codeAnalysisVersion )) {

        # Assert that the assembly is loaded and load it if not
        if ( Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path $assemblyLoadPath -ChildPath 'Kestrun.dll')) {
            # Load & register your DLL folders (as before):
            [Kestrun.Utilities.AssemblyAutoLoader]::PreloadAll($false, @($assemblyLoadPath))

            # When the runspace or script is finished:
            [Kestrun.Utilities.AssemblyAutoLoader]::Clear($true)   # remove hook + folders
        }
    }
} else {
    # Assert that the assembly is loaded and load it if not
    Assert-KrAssemblyLoaded (Join-Path -Path $assemblyLoadPath -ChildPath 'Kestrun.dll')
}

try {
    # Check if Kestrun assembly is already loaded
    if (-not ([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'Kestrun' } )) {
        throw 'Kestrun assembly is not loaded.'
    }
    if ([KestrunAnnotationsRuntimeInfo]::IsReleaseDistribution) {
        . "$moduleRootPath/Public-Route.ps1"
        . "$moduleRootPath/Public-Definition.ps1"
    } else {
        # load public functions
        Get-ChildItem "$($moduleRootPath)/Public/*.ps1" -Recurse | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }
    }

    # get functions from memory and compare to existing to find new functions added
    $funcs = Get-ChildItem Function: | Where-Object { $sysfuncs -notcontains $_ }

    if ($inRouteRunspace) {
        # set the function by context to the current runspace
        $funcs = Get-KrCommandsByContext -AnyOf Runtime -Functions $funcs
    }

    $aliases = Get-ChildItem Alias: | Where-Object { $sysaliases -notcontains $_ }
    # export the module's public functions
    if ($funcs) {
        if ($aliases) {
            Export-ModuleMember -Function ($funcs.Name) -Alias $aliases.Name
        } else {
            Export-ModuleMember -Function ($funcs.Name)
        }
    }

    if (-not $inRouteRunspace) {
        # Set the Kestrun root path for the host manager
        [Kestrun.KestrunHostManager]::KestrunRoot = $PWD
    }
} catch {
    throw ("Failed to import Kestrun module: $_")
} finally {
    # Cleanup temporary variables
    Remove-Variable -Name 'assemblyLoadPath', 'moduleRootPath', 'netVersion', 'codeAnalysisVersion', 'inRouteRunspace' , 'sysfuncs', 'sysaliases', 'funcs', 'aliases' -ErrorAction SilentlyContinue
}
