<#
    .SYNOPSIS
        Writes key/value data to the HTTP response body as application/x-www-form-urlencoded.
    .DESCRIPTION
        Uses the ASP.NET Core built-in System.Net.Http.FormUrlEncodedContent encoder
        to convert the provided object into an application/x-www-form-urlencoded
        payload, then writes it to the HTTP response.
    .PARAMETER InputObject
        Hashtable, PSCustomObject, dictionary, or any object with public properties.
    .PARAMETER StatusCode
        HTTP status code for the response.
    .PARAMETER ContentType
        Defaults to 'application/x-www-form-urlencoded'.
    .EXAMPLE
        @{ user='alice'; role='admin' } | Write-KrFormUrlEncodedResponse
#>
function Write-KrFormUrlEncodedResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object]$InputObject,

        [Parameter()]
        [int]$StatusCode = 200
    )

    begin {
        $items = [System.Collections.Generic.List[object]]::new()
    }

    process {
        $items.Add($InputObject)
    }

    end {
        if ($null -eq $Context -or $null -eq $Context.Response) {
            Write-KrOutsideRouteWarning
            return
        }

        $payload = if ($items.Count -eq 1) { $items[0] } else { $items }

        # Call the C# method directly with arguments
        $Context.Response.WriteFormUrlEncodedResponse($payload, $StatusCode)
    }
}
