<#
    .SYNOPSIS
        Sets only the HTTP status code for the response, without a body.
    .DESCRIPTION
        Sets the HTTP status code for the response and clears any body or content type,
        allowing status code pages middleware to handle the response body if configured.
    .PARAMETER StatusCode
        The HTTP status code to set for the response.
    .EXAMPLE
        Write-KrStatusResponse -StatusCode 404
        Sets the response status code to 404 Not Found, without a body. If status code pages
        middleware is enabled, it will generate the response body.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrStatusResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [int]$StatusCode
    )

    # Only works inside a route script block where $Context is available
    if ($null -eq $Context -or $null -eq $Context.Response) {
        Write-KrOutsideRouteWarning
        return
    }

    # Write only the status code, letting any status code pages middleware handle the response body
    $Context.Response.WriteStatusOnly($StatusCode)
}

