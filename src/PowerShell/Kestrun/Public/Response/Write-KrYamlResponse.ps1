<#
    .SYNOPSIS
        Writes an object to the HTTP response body as YAML.

    .DESCRIPTION
        Serializes the provided object to YAML using the underlying C# helper and
        sets the specified status code on the response.
    .PARAMETER InputObject
        The object to serialize and write to the response body. This can be any
        PowerShell object, including complex types.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "application/yaml".
    .EXAMPLE
        Write-KrYamlResponse -InputObject $myObject -StatusCode 200 -ContentType "application/x-yaml"
        Writes the $myObject serialized as YAML to the response with a 200 status code
        and content type "application/x-yaml".
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrYamlResponse {
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

        # Write the YAML response
        $Context.Response.WriteYamlResponse($payload, $StatusCode, $ContentType)
    }
}

