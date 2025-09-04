<#
    .SYNOPSIS
        Writes an object serialized as BSON to the HTTP response.
    .DESCRIPTION
        Converts the provided object to BSON format and writes it to the response body. The status code and content type can be customized.
    .PARAMETER InputObject
        The object to serialize and write to the response.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200.
    .PARAMETER ContentType
        The content type to set for the response. If not specified, defaults to application/bson.
    .EXAMPLE
        Write-KrBsonResponse -InputObject $myObject -StatusCode 200 -ContentType "application/bson"
        Writes the $myObject serialized as BSON to the response with a 200 status code and content type "application/bson".
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrBsonResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteBsonResponse($InputObject, $StatusCode, $ContentType)
    }
}

