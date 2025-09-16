<#
    .SYNOPSIS
        Writes an object to the HTTP response body as JSON.
    .DESCRIPTION
        Serializes the provided object to JSON using Newtonsoft.Json and writes it
        to the current HTTP response. The caller can specify the HTTP status code,
        serialization depth and formatting options.
    .PARAMETER InputObject
        The object to serialize and write to the response.
    .PARAMETER StatusCode
        The HTTP status code to set for the response.
    .PARAMETER Depth
        The maximum depth of the JSON serialization.
    .PARAMETER Compress
        Whether to compress the JSON output.
    .PARAMETER ContentType
        The content type of the response.
    .EXAMPLE
        PS> $myObject | Write-KrJsonResponse -StatusCode 201 -Depth 5 -Compress -ContentType "application/json"
        Serializes the object to JSON and writes it to the response with the specified options.
    .EXAMPLE
        PS> $myObject | Write-KrJsonResponse -StatusCode 400 -Depth 3 -Compress -ContentType "application/json"
        Serializes the object to JSON and writes it to the response with the specified options.
    .EXAMPLE
        PS> $myObject | Write-KrJsonResponse -StatusCode 500 -Depth 2
        Serializes the object to JSON and writes it to the response with the specified options.
#>
function Write-KrJsonResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object]$InputObject,

        [Parameter()]
        [int]$StatusCode = 200,

        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$Depth = 10,

        [Parameter()]
        [switch]$Compress,

        [Parameter()]
        [string]$ContentType
    )
    begin {
        # Collect all piped items
        $items = [System.Collections.Generic.List[object]]::new()
        $ContentType = [string]::IsNullOrEmpty($ContentType) ? 'application/json' : $ContentType
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

        $json = ConvertTo-Json -InputObject $payload -Depth $Depth -Compress:$Compress
        # Write the JSON response
        $Context.Response.WriteTextResponse($json, $StatusCode, $ContentType)
    }
}

