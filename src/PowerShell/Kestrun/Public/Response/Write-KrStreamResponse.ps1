<#
    .SYNOPSIS
        Writes a stream directly to the HTTP response body.
    .DESCRIPTION
        Copies the provided stream to the response output stream. Useful for
        forwarding large files or custom streaming scenarios.
    .PARAMETER InputObject
        The stream to write to the response body. This should be a valid stream object.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "application/octet-stream".
    .EXAMPLE
        Write-KrStreamResponse -InputObject $myStream -StatusCode 200 -ContentType "application/octet-stream"
        Writes the $myStream to the response body with a 200 OK status code and content type "application/octet-stream".
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrStreamResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteStreamResponse($InputObject, $StatusCode, $ContentType)
    }
}
