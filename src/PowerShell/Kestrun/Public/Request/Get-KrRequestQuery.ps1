<#
    .SYNOPSIS
        Retrieves a query parameter value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified query parameter by name.
    .PARAMETER Name
        The name of the query parameter to retrieve from the HTTP request.
    .EXAMPLE
        $value = Get-KrRequestQuery -Name "param1"
        Retrieves the value of the query parameter "param1" from the HTTP request.
    .OUTPUTS
        Returns the value of the specified query parameter, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestQuery {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    if ($null -ne $Context.Request) {
        # Get the query parameter value from the request
        return $Context.Request.Query[$Name]
    }
}

