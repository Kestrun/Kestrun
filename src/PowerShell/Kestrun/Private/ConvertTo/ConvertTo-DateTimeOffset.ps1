<#
.SYNOPSIS
    Converts various input types to [DateTimeOffset].
.DESCRIPTION
    Accepts input as:
        - [DateTimeOffset] instances
        - [DateTime] instances (converted to local DateTimeOffset)
        - Strings (parsed into DateTimeOffset or interpreted as duration from now)
        - [TimeSpan] instances (added to current time)
        - Numeric values (interpreted as seconds from now)
.PARAMETER InputObject
    The input value to convert to a DateTimeOffset.
.EXAMPLE
    # From DateTimeOffset
    $dto = [DateTimeOffset]::Now
    ConvertTo-DateTimeOffset -InputObject $dto

.EXAMPLE
    # From DateTime
    $dt = [DateTime]::Now
    ConvertTo-DateTimeOffset -InputObject $dt

.EXAMPLE
    # From string
    ConvertTo-DateTimeOffset -InputObject "2025-09-10T23:00Z"

.EXAMPLE
    # From TimeSpan
    $ts = [TimeSpan]::FromHours(1)
    ConvertTo-DateTimeOffset -InputObject $ts

.EXAMPLE
    # From numeric seconds
    ConvertTo-DateTimeOffset -InputObject 3600
.OUTPUTS
    System.DateTimeOffset
#>
function ConvertTo-DateTimeOffset {
    [CmdletBinding()]
    [OutputType('System.DateTimeOffset')]
    param(
        [Parameter(Mandatory)]
        [object]$InputObject
    )

    # Already DateTimeOffset
    if ($InputObject -is [DateTimeOffset]) { return $InputObject }

    # DateTime -> DateTimeOffset (local)
    if ($InputObject -is [DateTime]) {
        return [DateTimeOffset]::new([DateTime]$InputObject)
    }

    # String → try absolute date/time first
    if ($InputObject -is [string]) {
        $s = $InputObject.Trim()
        $dto = $null
        if ([DateTimeOffset]::TryParse($s, [ref]$dto)) { return $dto }

        # If not a date, try treating the string as a duration and add to now
        try {
            $ts = ConvertTo-TimeSpan -InputObject $s
            return [DateTimeOffset]::Now.Add($ts)
        } catch {
            throw "Invalid Expires value '$s'. Provide an absolute date (e.g. '2025-09-10T23:00Z') or a duration (e.g. '2h', '1d')."
        }
    }

    # TimeSpan => relative expiry from now
    if ($InputObject -is [TimeSpan]) {
        return [DateTimeOffset]::Now.Add([TimeSpan]$InputObject)
    }

    # Numeric seconds => relative expiry
    if ($InputObject -is [int] -or $InputObject -is [long] -or $InputObject -is [double] -or $InputObject -is [decimal]) {
        return [DateTimeOffset]::Now.AddSeconds([double]$InputObject)
    }

    throw "Cannot convert value of type [$($InputObject.GetType().FullName)] to [DateTimeOffset]."
}
