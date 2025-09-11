<#
    .SYNOPSIS
        Writes CSV data to the HTTP response body.
    .DESCRIPTION
        Sends a raw CSV payload to the client and optionally sets the HTTP status
        code and content type.
    .PARAMETER InputObject
        The CSV content to write to the response body. This can be a string or any
        other object that can be converted to a string.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).
    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "text/csv".
    .PARAMETER CsvConfiguration
        An optional CsvHelper configuration object to customize CSV serialization.
    .EXAMPLE
        Write-KrCsvResponse -InputObject "Name,Age`nAlice,30`nBob,25" -StatusCode 200
        Writes the CSV data to the response body with a 200 OK status code.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrCsvResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType,
        [Parameter()]
        [CsvHelper.Configuration.CsvConfiguration] $CsvConfiguration = $null
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        $Context.Response.WriteCsvResponse($InputObject, $StatusCode, $ContentType, $CsvConfiguration)
    } else {
        Write-KrOutsideRouteWarning
    }
}

