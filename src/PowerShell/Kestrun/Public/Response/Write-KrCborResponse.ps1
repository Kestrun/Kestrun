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
        $Context.Response.WriteCborResponse($payload, $StatusCode, $ContentType)
    }
}

