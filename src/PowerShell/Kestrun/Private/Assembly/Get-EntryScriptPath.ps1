<#
.SYNOPSIS
    Gets the path of the entry script that invoked the current script.
.DESCRIPTION
    This function inspects the PowerShell call stack to determine the script path
    of the entry script that invoked the current script. It excludes the current
    script's path from consideration.
.OUTPUTS
    [string] - The path of the entry script, or $null if not found.
.NOTES
    This function is part of the Kestrun PowerShell module.
#>
function Get-EntryScriptPath {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $self = $PSCommandPath ? (Resolve-Path -LiteralPath $PSCommandPath).ProviderPath : $null

    # Take the last real script caller in the stack
    $stack = @(Get-PSCallStack)
    [System.Array]::Reverse($stack)
    foreach ($f in $stack) {
        $p = $f.InvocationInfo.ScriptName
        if (-not $p) { continue }

        try {
            $resolved = (Resolve-Path -LiteralPath $p -ErrorAction Stop).ProviderPath
        } catch {
            Write-Debug "Failed to resolve path '$p': $_"
            continue
        }

        if ($resolved -and $resolved -ne $self) {
            return $resolved
        }
    }

    return $null
}
