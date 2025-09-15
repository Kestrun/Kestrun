<#
    .SYNOPSIS
        Writes plain text to the HTTP response body.

    .DESCRIPTION
        Sends a raw text payload to the client and optionally sets the HTTP status
        code and content type.

    .PARAMETER InputObject
        The text content to write to the response body. This can be a string or any
        other object that can be converted to a string.

    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).

    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "text/plain".

    .EXAMPLE
        Write-KrTextResponse -InputObject "Hello, World!" -StatusCode 200
        Writes "Hello, World!" to the response body with a 200 OK status code.

    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrTextResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [Alias('Text')]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteTextResponse($InputObject, $StatusCode, $ContentType)
    } else {
        Write-KrOutsideRouteWarning
    }
}

