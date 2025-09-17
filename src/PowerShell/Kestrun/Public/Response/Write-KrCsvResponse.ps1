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
    .PARAMETER Delimiter
        The character to use as the delimiter in the CSV output. Defaults to a comma (`,`).
    .PARAMETER IncludeTypeInformation
        Switch to include type information in the CSV output.
    .PARAMETER QuoteFields
        An array of field names to always quote in the CSV output.
    .PARAMETER UseQuotes
        Specifies how to quote fields in the CSV output. Accepts values from the
        `Microsoft.PowerShell.Commands.BaseCsvWritingCommand+QuoteKind` enum.
    .PARAMETER NoHeader
        Switch to omit the header row from the CSV output.
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
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [string]$ContentType,
        [Parameter()]
        [char]$Delimiter,
        [Parameter()]
        [switch]$IncludeTypeInformation,
        [Parameter()]
        [string[]]$QuoteFields,
        [Parameter()]
        [Microsoft.PowerShell.Commands.BaseCsvWritingCommand+QuoteKind]$UseQuotes,
        [Parameter()]
        [switch]$NoHeader
    )
    begin {
        # Collect all piped items
        $items = @()
        $ContentType = [string]::IsNullOrEmpty($ContentType) ? 'text/csv' : $ContentType
    }
    process {
        # Accumulate; no output yet
        $items += $InputObject
    }
    end {
        # Only works inside a route script block where $Context is available
        if ($null -eq $Context -or $null -eq $Context.Response) {
            Write-KrOutsideRouteWarning
            return
        }
        #  - single item by default when only one was piped
        #  - array if multiple items were piped

        $params = @{}
        if ($Delimiter) { $params['Delimiter'] = $Delimiter }
        if ($IncludeTypeInformation.IsPresent) { $params['IncludeTypeInformation'] = $true }
        if ($QuoteFields) { $params['QuoteFields'] = $QuoteFields }
        if ($UseQuotes) { $params['UseQuotes'] = $UseQuotes }
        if ($NoHeader.IsPresent) { $params['NoHeader'] = $true }

        # Convert the payload to CSV
        $csv = $items | ConvertTo-Csv @params
        # Write the CSV response
        $Context.Response.WriteTextResponse($csv -join "`n", $StatusCode, $ContentType)
    }
}

