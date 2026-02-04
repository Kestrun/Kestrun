<#
.SYNOPSIS
    Retrieves a form option from the Kestrun server.
.DESCRIPTION
    The Get-KrFormOption function retrieves a Kestrun form option by its name from the server's form options collection.
.PARAMETER Name
    The name of the form option to retrieve.
.OUTPUTS
    Kestrun.Forms.KrFormOptions
.EXAMPLE
    $formOption = Get-KrFormOption -Name 'MyFormOption'
    This example retrieves the form option named 'MyFormOption' from the Kestrun server.
.NOTES
    This function is part of the Kestrun.Forms module and is used to define form options.
#>
function Get-KrFormOption {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Forms.KrFormOptions])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name
    )
    $Server = Resolve-KestrunServer

    # Return the form option by name or null if not found
    return $Server.GetFormOption($Name)
}
