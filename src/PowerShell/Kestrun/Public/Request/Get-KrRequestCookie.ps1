<#
    .SYNOPSIS
        Retrieves a cookie value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified cookie by name.
    .PARAMETER Name
        The name of the cookie to retrieve from the HTTP request.
    .EXAMPLE
        $value = Get-KrRequestCookie -Name "param1"
        Retrieves the value of the cookie "param1" from the HTTP request.
    .OUTPUTS
        Returns the value of the specified cookie, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestCookie {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    if ($null -ne $Context.Request) {
        # Get the cookie value from the request
        return $Context.Request.Cookies[$Name]
    }
}

