<#
    .SYNOPSIS
        Retrieves a request header value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified request header by name.
    .PARAMETER Name
        The name of the request header to retrieve from the HTTP request.
    .EXAMPLE
        $value = Get-KrRequestHeader -Name "param1"
        Retrieves the value of the request header "param1" from the HTTP request.
    .OUTPUTS
        Returns the value of the specified request header, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestHeader {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    if ($null -ne $Context.Request) {
        # Get the request header value from the request
        return $Context.Request.Headers[$Name]
    }
}

