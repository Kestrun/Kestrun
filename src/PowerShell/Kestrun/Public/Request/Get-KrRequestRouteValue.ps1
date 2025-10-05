<#
    .SYNOPSIS
        Retrieves a request route value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified request route value by name.
    .PARAMETER Name
        The name of the request route value to retrieve from the HTTP request.
    .EXAMPLE
        $value = Get-KrRequestRouteParam -Name "param1"
        Retrieves the value of the request route value "param1" from the HTTP request.
    .OUTPUTS
        Returns the value of the specified request route value, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestRouteParam {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    if ($null -ne $Context) {
        # Prefer live HttpContext route values so re-executed requests (StatusCodePagesWithReExecute)
        # see the updated route data. Fall back to snapshot on KestrunRequest.
        $live = $Context.HttpContext?.Request?.RouteValues
        if ($null -ne $live) {
            $val = $live[$Name]
            if ($null -ne $val) { return $val }
        }

        # Fallback to KestrunRequest snapshot
        if ($null -ne $Context.Request -and $null -ne $Context.Request.RouteValues) {
            return $Context.Request.RouteValues[$Name]
        }
    }
}

