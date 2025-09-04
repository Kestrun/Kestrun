<#
    .SYNOPSIS
        Selects which CPython runtime pythonnet will embed.

    .DESCRIPTION
        • With no -Path the newest 64-bit CPython is auto-discovered.
        • If the env-var PYTHONNET_PYDLL is already present the function
        exits immediately—unless you pass -Force (or give an explicit -Path).
        • Applies the setting both to the environment and to the live
        [Python.Runtime.Runtime]::PythonDLL property when possible.

    .PARAMETER Path
        Full path to pythonXY.dll / libpythonX.Y.so / libpythonX.Y.dylib.

    .PARAMETER Force
        Override an existing PYTHONNET_PYDLL environment variable.

    .EXAMPLE
        # Leave current setting untouched if already configured
        Set-KrPythonRuntime

    .EXAMPLE
        # Override whatever is set and pin CPython 3.12
        Set-KrPythonRuntime -Path '/opt/python312/lib/libpython3.12.so' -Force
#>
function Set-KrPythonRuntime {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [string] $Path,
        [switch] $Force
    )

    # ------------------------------------------------------------
    # 0. Does pythonnet already know a valid DLL / .so / .dylib?
    # ------------------------------------------------------------
    $currentDll = [Python.Runtime.Runtime]::PythonDLL

    if (-not $Force -and -not $Path -and
        $currentDll -and (Test-Path $currentDll)) {

        Write-Verbose "pythonnet already configured → $currentDll"
        return (Resolve-Path $currentDll).Path
    }

    # ------------------------------------------------------------
    # 1. If caller didn’t supply -Path, auto-discover
    # ------------------------------------------------------------
    if (-not $Path) {
        if ($IsWindows) {
            # Windows: take the DLL next to the first python.exe on PATH
            $pyExe = (Get-Command python.exe, python3.exe -ErrorAction Ignore |
                    Select-Object -First 1).Source
            if ($pyExe) {
                $Path = Get-ChildItem (Join-Path (Split-Path $pyExe) 'python*.dll') -ErrorAction Ignore |
                    Sort-Object VersionInfo.FileVersion -Descending |
                    Select-Object -First 1 -Expand FullName
            }
        } else {
            # Linux / macOS: ask whereis for libpython3*.so / .dylib
            $pattern = if ($IsMacOS) { 'libpython3*.dylib' } else { 'libpython3*.so' }
            $Path = & whereis -b $pattern 2>$null |
                Select-String -Pattern $pattern |
                ForEach-Object { $_.ToString().Split(' ', 2)[1] } |
                Sort-Object Length | Select-Object -First 1
        }
    }

    if (-not $Path -or -not (Test-Path $Path)) {
        throw 'Could not locate a CPython runtime. Install Python ≥3.11 or supply -Path (-Force overrides existing setting).'
    }

    # ------------------------------------------------------------
    # 2. Tell pythonnet to use this runtime
    # ------------------------------------------------------------
    $Path = (Resolve-Path $Path).Path
    Write-Verbose "pythonnet will use: $Path"
    # If pythonnet already loaded: update in-process
    [Python.Runtime.Runtime]::PythonDLL = $Path

    return $Path
}
