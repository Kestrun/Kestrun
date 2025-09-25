
<#
    .SYNOPSIS
        Resolves a file path relative to the Kestrun root or a specified base path.
    .DESCRIPTION
        This function expands environment variables and resolves the provided path against the Kestrun root or a specified base path.
        If the path is relative, it combines it with the base path. If the -Test switch is used, it checks if the resolved path exists and returns the original input path if it does not.
    .PARAMETER Path
        The path to resolve. This can be an absolute path or a relative path.
    .PARAMETER RelativeBasePath
        An optional base path to resolve the relative path against. If not specified, the current directory is used.
    .PARAMETER KestrunRoot
        If specified, the Kestrun root directory is used as the base path for resolving the relative path.
    .PARAMETER Test
        If specified, the function will check if the resolved path exists. If it does not, the original input path is returned instead of the resolved path.
    .EXAMPLE
        Resolve-KrPath -Path "~/Documents/file.txt" -KestrunRoot
        Resolves the path "~/Documents/file.txt" relative to the Kestrun root directory, expanding any environment variables.
    .EXAMPLE
        Resolve-KrPath -Path "file.txt" -RelativeBasePath "C:\Base\Path"
        Resolves the path "file.txt" relative to "C:\Base\Path", expanding any environment variables.
    .NOTES
        This function is designed to be used in the context of a Kestrun server to resolve file paths correctly.
#>
function Resolve-KrPath {
    [CmdletBinding()]
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline)]
        [string] $Path,

        [Parameter(parameterSetName = 'RelativeBasePath')]
        [string] $RelativeBasePath,

        [Parameter(parameterSetName = 'KestrunRoot')]
        [switch] $KestrunRoot,

        [Parameter()]
        [switch] $Test
    )
    process {
        # --- 1. Expand ~/env in both Path and, if supplied, RelativeBasePath ---
        $expand = {
            param($p)
            if ($p -like '~*') {
                $p = $p -replace '^~', $HOME
            }
            [Environment]::ExpandEnvironmentVariables($p)
        }

        $p3 = & $expand $Path

        if ($KestrunRoot) {
            # Use the Kestrun root as base
            $RelativeBasePath = [Kestrun.KestrunHostManager]::KestrunRoot
        }

        if ($RelativeBasePath) {
            # Expand + normalize the base, even if it doesn't exist
            $base3 = & $expand $RelativeBasePath
            $baseFull = [IO.Path]::GetFullPath($base3)

            # If $Path is rooted, ignore the base; else combine
            if ([IO.Path]::IsPathRooted($p3)) {
                $full = [IO.Path]::GetFullPath($p3)
            } else {
                $combined = [IO.Path]::Combine($baseFull, $p3)
                $full = (Resolve-Path -Path ([IO.Path]::GetFullPath($combined))).Path
            }
        } else {
            # No base supplied: just make absolute against current directory
            $full = [IO.Path]::GetFullPath($p3)
        }

        # --- 4. If -Test was used and file doesn't exist, return the original input Path ---
        if ($Test -and -not (Test-Path $full)) {
            return $Path
        }

        return $full
    }
}

