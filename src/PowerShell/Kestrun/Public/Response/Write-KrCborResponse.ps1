<#
    .SYNOPSIS
        Writes an object serialized as CBOR to the HTTP response.
    .DESCRIPTION
        Converts the provided object to CBOR format and writes it to the response body. The status code and content type can be customized.
    .PARAMETER InputObject
        The object to serialize and write to the response.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200.
    .PARAMETER ContentType
        The content type to set for the response. If not specified, defaults to application/cbor
    .EXAMPLE
        Write-KrCborResponse -InputObject $myObject -StatusCode 200 -ContentType "application/cbor"
        Writes the $myObject serialized as CBOR to the response with a 200 status code and
        content type "application/cbor".
#>
function Write-KrCborResponse {
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
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteCborResponse($InputObject, $StatusCode, $ContentType)
    } else {
        Write-KrOutsideRouteWarning
    }
}

