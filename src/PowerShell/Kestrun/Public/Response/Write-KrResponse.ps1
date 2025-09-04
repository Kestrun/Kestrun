<#
    .SYNOPSIS
        Writes a response to the HTTP client.
    .DESCRIPTION
        This function is a wrapper around the Kestrun server response methods.
    .PARAMETER InputObject
        The input object to write to the response body. This can be a stream, byte array, or other types.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "application/octet-stream".
    .EXAMPLE
        Write-KrResponse -InputObject $myStream -StatusCode 200 -ContentType "application/octet-stream"
        Writes the $myStream to the response body with a 200 OK status code and content type "application/octet-stream".
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200
    )
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteResponse($InputObject, $StatusCode, $ContentType)
    }
}

