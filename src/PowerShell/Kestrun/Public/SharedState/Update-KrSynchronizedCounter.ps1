<#
.SYNOPSIS
    Updates a synchronized counter in a thread-safe manner.
.DESCRIPTION
    This function updates a numeric counter stored in a synchronized collection
    (such as a synchronized Hashtable or OrderedDictionary) in a thread-safe way.
    It uses locking to ensure that increments are atomic and safe for concurrent access.
.PARAMETER Table
    The synchronized collection (Hashtable or OrderedDictionary) containing the counter.
.PARAMETER Key
    The key in the collection that identifies the counter to update.
.PARAMETER By
    The amount to increment the counter by. Default is 1.
.PARAMETER WhatIf
    Shows what would happen if the command runs. The command is not executed.
.PARAMETER Confirm
    Prompts you for confirmation before running the command. The command is not run unless you respond
    affirmatively.
.EXAMPLE
    $table = [hashtable]::Synchronized(@{ Visits = 0 })
    Update-KrSynchronizedCounter -Table $table -Key 'Visits' -By 1
    This increments the 'Visits' counter in the synchronized hashtable by 1.
.NOTES
    This function is part of the Kestrun.SharedState module and is used to safely update
    counters in shared state.
#>
function Update-KrSynchronizedCounter {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$Table,   # Hashtable or OrderedDictionary (synchronized)

        [Parameter(Mandatory)]
        [string]$Key,

        [Parameter()]
        [int]$By = 1   # default increment is +1
    )

    $lock = $Table.SyncRoot

    $target = "counter '$Key' in synchronized table"
    $action = "Update by $By"

    if (-not $PSCmdlet.ShouldProcess($target, $action)) {
        # Return current value (or $null if missing) when -WhatIf / -Confirm says "no"
        return $Table[$Key]
    }

    [System.Threading.Monitor]::Enter($lock)
    try {
        if (-not $Table.Contains($Key)) {
            $Table[$Key] = 0
        }

        $Table[$Key] = [int]$Table[$Key] + $By
        return $Table[$Key]
    } finally {
        [System.Threading.Monitor]::Exit($lock)
    }
}
