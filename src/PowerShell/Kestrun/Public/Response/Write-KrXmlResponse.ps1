<#
    .SYNOPSIS
        Writes an object serialized as XML to the HTTP response.

    .DESCRIPTION
        Converts the provided object to XML and writes it to the response body. The
        status code and content type can be customized.
    .PARAMETER InputObject
        The object to serialize and write to the response body. This can be any
        PowerShell object, including complex types.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "application/xml".
    .EXAMPLE
        Write-KrXmlResponse -InputObject $myObject -StatusCode 200 -ContentType "application/kestrun-xml"
        Writes the $myObject serialized as XML (<kestrun-xml>) to the response with a 200 status code
        and content type "application/kestrun-xml".
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrXmlResponse {
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

        # Write the XML response
        $Context.Response.WriteXmlResponse($payload, $StatusCode, $ContentType)
    }
}

