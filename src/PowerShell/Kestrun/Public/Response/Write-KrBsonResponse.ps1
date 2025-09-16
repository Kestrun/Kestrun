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
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType
    )
    begin {
        # Collect all piped items
        $items = [System.Collections.Generic.List[object]]::new()
    }
    process {
        # Accumulate; no output yet
        $items.Add($InputObject)
    }
    end {
        # Only works inside a route script block where $Context is available
        if ($null -eq $Context -or $null -eq $Context.Response) {
            Write-KrOutsideRouteWarning
            return
        }
        #  - single item by default when only one was piped
        #  - array if multiple items were piped
        $payload = if ($items.Count -eq 1) { $items[0] } else { $items.ToArray() }

        # Write the CBOR response
        $Context.Response.WriteBsonResponse($payload, $StatusCode, $ContentType)
    }
}

