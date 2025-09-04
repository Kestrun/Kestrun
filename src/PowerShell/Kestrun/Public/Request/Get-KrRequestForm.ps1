<#
    .SYNOPSIS
        Retrieves a request form value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the request form.
    .EXAMPLE
        $value = Get-KrRequestForm
        Retrieves the value of the request form from the HTTP request.
    .OUTPUTS
        Returns the value of the request form, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestForm {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param()
    if ($null -ne $Context.Request) {
        # Get the request body value from the request
        return $Context.Request.Form
    }
}

