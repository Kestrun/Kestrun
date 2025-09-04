<#
    .SYNOPSIS
        Writes binary data directly to the HTTP response body.
    .DESCRIPTION
        Sends a byte array to the client. Useful for returning images or other
        binary content with a specified status code and content type.
    .PARAMETER InputObject
        The binary data to write to the response body. This should be a byte array.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "application/octet-stream".
    .EXAMPLE
        Write-KrBinaryResponse -InputObject $myBinaryData -StatusCode 200 -ContentType "application/octet-stream"
        Writes the $myBinaryData to the response body with a 200 OK status code and
        content type "application/octet-stream".
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrBinaryResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteBinaryResponse($InputObject, $StatusCode, $ContentType)
    }
}

