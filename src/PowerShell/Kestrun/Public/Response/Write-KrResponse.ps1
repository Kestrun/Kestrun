<#
    .SYNOPSIS
        Writes a response with the specified input object and HTTP status code.
    .DESCRIPTION
        This function is a wrapper around the Kestrun server response methods.
        The response format based on the Accept header or defaults to text/plain.
        Content type is determined automatically.
    .PARAMETER InputObject
        The input object to write to the response body.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).

    .EXAMPLE
        Write-KrResponse -InputObject $myObject -StatusCode 200
        Writes the $myObject to the response with a 200 status code. The content type
        is determined automatically based on the Accept header or defaults to text/plain.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteResponse($InputObject, $StatusCode)
    } else {
        Write-KrOutsideRouteWarning
    }
}

