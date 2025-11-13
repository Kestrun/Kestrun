<#
.SYNOPSIS
    Removes a global variable from Kestrun shared state.
.DESCRIPTION
    Deletes a variable from the Kestrun global variable table.
    If the variable does not exist, no action is taken.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Global
    If specified, the variable is removed from the global shared state.
.PARAMETER Name
    Name of the variable to remove.
.PARAMETER WhatIf
    Shows what would happen if the command runs. The command is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the command. The command is not run unless you respond
    affirmatively.
.EXAMPLE
    Remove-KrSharedState -Name "MyVariable"
    This removes the global variable "MyVariable".
.NOTES
    This function is part of the Kestrun.SharedState module and is used to remove global variables.
#>
function Remove-KrSharedState {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(defaultParameterSetName = 'Server', SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    [OutputType([bool])]
    param(
        [Parameter(ValueFromPipeline = $true, ParameterSetName = 'Server')]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Global')]
        [switch]$Global,

        [Parameter(Mandatory)]
        [string]$Name
    )
    begin {
        if (-not $Global.IsPresent) {
            # Ensure the server instance is resolved
            $Server = Resolve-KestrunServer -Server $Server
        }
    }
    process {
        if ($Global.IsPresent) {
            # Remove from global store
            if ($PSCmdlet.ShouldProcess("Global shared state variable '$Name'", "Remove")) {
                return [Kestrun.SharedState.GlobalStore].Remove($Name)
            }
            return $false
        }
        # Remove from server instance
        if ($PSCmdlet.ShouldProcess("Shared state variable '$Name' on server '$($Server.Name)'", "Remove")) {
            return $Server.SharedState.Remove($Name)
        }
        return $false
    }
}

