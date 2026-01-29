param(
    [string] $AspNetCoreVersion
)

$KrAspNetCoreVersion = if ([System.String]::IsNullOrWhiteSpace($AspNetCoreVersion)) {
    $null
} else {
    if ($AspNetCoreVersion -notin @('net8.0', 'net9.0', 'net10.0')) {
        throw "Invalid ASP.NET Core version specified: $AspNetCoreVersion. Valid values are 'net8.0', 'net9.0', 'net10.0'."
    }
    $AspNetCoreVersion
}

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

# Pick TFM by actual runtime (not PS minor)
$fx = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription

if ($fx -match '\.NET\s+10\.') {
    if ([string]::IsNullOrWhiteSpace($KrAspNetCoreVersion)) {
        # Default to net10.0 for .NET 10 runtime
        $KrAspNetCoreVersion = 'net10.0'
    }
    $codeAnalysisVersion = '4.14.0'   # keep consistent with your csproj net10 group
} elseif ($fx -match '\.NET\s+9\.') {
    if ([string]::IsNullOrWhiteSpace($KrAspNetCoreVersion)) {
        # Default to net8.0 for PowerShell 7.5 on .NET 9 runtime
        $KrAspNetCoreVersion = ($PSVersionTable.PSVersion.Major -eq 7 -and $PSVersionTable.PSVersion.Minor -eq 5) ? 'net8.0' : 'net9.0'  # force downgrade for pwsh 7.5
    }
    $codeAnalysisVersion = '4.11.0'   # matches your net9 group
} elseif ($fx -match '\.NET\s+8\.') {
    if ([string]::IsNullOrWhiteSpace($KrAspNetCoreVersion)) {
        # Default to net8.0 for .NET 8 runtime
        $KrAspNetCoreVersion = 'net8.0'
    }
    $codeAnalysisVersion = '4.9.2'    # matches your net8 group
} else {
    throw "Unsupported .NET runtime host: $fx. Please use pwsh on .NET 8/9/10."
}

$publicRoutePath = (Join-Path -Path $moduleRootPath -ChildPath 'Public-Route.ps1')
$publicDefinitionPath = (Join-Path -Path $moduleRootPath -ChildPath 'Public-Definition.ps1')
$privatePath = (Join-Path -Path $moduleRootPath -ChildPath 'Private.ps1')
$SignModuleFile = ((Test-Path $publicRoutePath) -and (Test-Path $publicDefinitionPath) -and (Test-Path $privatePath))

# Determine if this is a release distribution
if ( ([KestrunAnnotationsRuntimeInfo]::IsReleaseDistribution) -and $SignModuleFile) {
    # Load private functions
    . "$privatePath"
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
$assemblyLoadPath = Join-Path -Path (Join-Path -Path $moduleRootPath -ChildPath 'lib') -ChildPath $KrAspNetCoreVersion

# Determine if we are in a route runspace by checking for the KrServer variable
$inRouteRunspace = $null -ne $ExecutionContext.SessionState.PSVariable.GetValue('KrServer')

if (-not $inRouteRunspace) {
    # Usage
    if ((Add-KrAspNetCoreType -Version $KrAspNetCoreVersion ) -and
        (Add-KrCodeAnalysisType -ModuleRootPath $moduleRootPath -Version $codeAnalysisVersion)) {

        # Assert that the assembly is loaded and load it if not
        if ( Assert-KrAssemblyLoaded -AssemblyPath (Join-Path -Path $assemblyLoadPath -ChildPath 'Kestrun.dll')) {
            # Load & register the DLL folders
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
    if ([KestrunAnnotationsRuntimeInfo]::IsReleaseDistribution -and $SignModuleFile) {
        . "$publicRoutePath"
        . "$publicDefinitionPath"
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
        [Kestrun.KestrunRuntimeInfo]::AspNetCoreVersion = $KrAspNetCoreVersion
    }
    <#

    # Register Type Accelerators for form payloads
    $ta = [psobject].Assembly.GetType('System.Management.Automation.TypeAccelerators')

    # Add KrFormData -> Kestrun.Forms.KrNamedPartsPayload
    if (-not $ta::Get.ContainsKey('KrFormData')) {
        $ta::Add('KrFormData', [Kestrun.Forms.KrNamedPartsPayload])
    }

    # Add KrMultipart -> Kestrun.Forms.KrOrderedPartsPayload
    if (-not $ta::Get.ContainsKey('KrMultipart')) {
        $ta::Add('KrMultipart', [Kestrun.Forms.KrOrderedPartsPayload])
    }#>
} catch {
    throw ("Failed to import Kestrun module: $_")
} finally {
    # Cleanup temporary variables
    Remove-Variable -Name 'assemblyLoadPath', 'moduleRootPath', 'netVersion', 'codeAnalysisVersion', 'inRouteRunspace' , 'sysfuncs', 'sysaliases', 'funcs', 'aliases' -ErrorAction SilentlyContinue
}
