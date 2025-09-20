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
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Alias('Text')]
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
        $Context.Response.WriteTextResponse($payload, $StatusCode, $ContentType)
    }
}

