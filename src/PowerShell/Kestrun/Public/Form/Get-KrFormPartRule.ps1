<#
.SYNOPSIS
    Retrieves a form part rule from the Kestrun server.
.DESCRIPTION
    The Get-KrFormPartRule function retrieves a Kestrun form part rule by its name from the server's form part rules collection.
.PARAMETER Name
    The name of the form part rule to retrieve.
.OUTPUTS
    Kestrun.Forms.KrFormPartRules
.EXAMPLE
    $formPartRule = Get-KrFormPartRule -Name 'MyFormPartRule'
    This example retrieves the form part rule named 'MyFormPartRule' from the Kestrun server.
.NOTES
    This function is part of the Kestrun.Forms module and is used to define form options.
#>
function Get-KrFormPartRule {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Forms.KrFormPartRule])]
    param(
        [Parameter()]
        [string] $Name
    )
    $Server = Resolve-KestrunServer

    # Return the form part rule by name or null if not found
    return $Server.GetFormPartRule($Name)
}
