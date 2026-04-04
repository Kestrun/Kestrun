<#
    .SYNOPSIS
        Returns modules imported by the user session.

    .DESCRIPTION
        Filters the list of currently loaded modules to exclude built-in PowerShell
        modules, Visual Studio Code host modules and Microsoft.PowerShell.* modules.
        The result represents modules explicitly imported by the user or scripts.
#>
function Get-KrUserImportedModule {
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSModuleInfo])]
    param()

    # ----- constants ----------------------------------------------------------
    $inboxRoot = [IO.Path]::GetFullPath( (Join-Path $PSHOME 'Modules') )

    # regex fragment that matches “…\.vscode\extensions\ms-vscode.powershell…”
    $vsCodeRegex = [Regex]::Escape(
        [IO.Path]::Combine('.vscode', 'extensions', 'ms-vscode.powershell')
    ) -replace '\\\\', '[\\/]'   # make path-separator agnostic
    # -------------------------------------------------------------------------

    Get-Module | Where-Object {
        $path = [IO.Path]::GetFullPath($_.ModuleBase)

        $isInbox = $path.StartsWith($inboxRoot,
            $IsWindows ? 'OrdinalIgnoreCase' : 'Ordinal')
        $isVSCode = $path -match $vsCodeRegex
        $isMSPSSpace = $_.Name -like 'Microsoft.PowerShell.*'

        -not ($isInbox -or $isVSCode -or $isMSPSSpace)
    }
}

