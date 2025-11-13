<#
.SYNOPSIS
    Retrieves the value of a previously defined global variable.
.DESCRIPTION
    Looks up a variable in the Kestrun global variable table and returns its
    value. If the variable does not exist, `$null` is returned.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Global
    If specified, the variable is retrieved from the global shared state.
.PARAMETER Name
    Name of the variable to retrieve.
    This should be the fully qualified name of the variable, including any
    namespaces.
.EXAMPLE
    Get-KrSharedState -Name "MyVariable"
    This retrieves the value of the global variable "MyVariable".
.NOTES
    This function is part of the Kestrun.SharedState module and is used to retrieve the value of global variables.
#>
function Get-KrSharedState {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(defaultParameterSetName = 'Server')]
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
            # Retrieve from server instance
            return [Kestrun.SharedState.GlobalStore].Get($Name)
        }
        # Retrieve (or $null if not defined)
        return $Server.SharedState.Get($Name)
    }
}

