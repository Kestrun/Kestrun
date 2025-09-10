<#
.SYNOPSIS
    Converts various input types to a [TimeSpan] instance.
.DESCRIPTION
    Accepts input as:
        - [TimeSpan] instances
        - Numeric values (interpreted as seconds)
        - Strings (parsed into TimeSpan)
    The string parser supports standard .NET TimeSpan formats (c, g, G) as well as compact
    token formats like "1d2h30m15s250ms" (order-insensitive, any subset).
.PARAMETER InputObject
    The input value to convert to a TimeSpan.
.EXAMPLE
    # From TimeSpan
    $ts = [TimeSpan]::FromHours(1.5)
    ConvertTo-TimeSpan -InputObject $ts

.EXAMPLE
    # From numeric seconds
    ConvertTo-TimeSpan -InputObject 90

.EXAMPLE
    # From string
    ConvertTo-TimeSpan -InputObject "1d2h30m15s250ms"
.OUTPUTS
    System.TimeSpan
#>
function ConvertTo-TimeSpan {
    [CmdletBinding()]
    [OutputType('System.TimeSpan')]
    param(
        [Parameter(Mandatory)]
        [object]$InputObject
    )

    # 1) Already a TimeSpan
    if ($InputObject -is [TimeSpan]) { return $InputObject }

    # 2) Numeric => seconds
    if ($InputObject -is [int] -or
        $InputObject -is [long] -or
        $InputObject -is [double] -or
        $InputObject -is [decimal]) {
        return [TimeSpan]::FromSeconds([double]$InputObject)
    }

    # 3) String parsing
    if ($InputObject -is [string]) {
        $s = $InputObject.Trim()

        # Try .NET built-in formats first: "c", "g", "G" (e.g., "00:30:00", "1.02:03:04")
        $ts = [TimeSpan]::Zero
        if ([TimeSpan]::TryParse($s, [ref]$ts)) { return $ts }

        # Compact tokens: 1d2h30m15s250ms (order-insensitive, any subset)
        if ($s -match '^(?i)(?:\s*(?<d>\d+)\s*d)?(?:\s*(?<h>\d+)\s*h)?(?:\s*(?<m>\d+)\s*m)?(?:\s*(?<s>\d+)\s*s)?(?:\s*(?<ms>\d+)\s*ms)?\s*$') {
            $days = [int]::Parse(('0' + $Matches['d']))
            $hrs = [int]::Parse(('0' + $Matches['h']))
            $min = [int]::Parse(('0' + $Matches['m']))
            $sec = [int]::Parse(('0' + $Matches['s']))
            $msec = [int]::Parse(('0' + $Matches['ms']))
            return [TimeSpan]::new($days, $hrs, $min, $sec, $msec)
        }

        throw "Invalid TimeSpan format: '$s'. Try '00:30:00', '1.02:03:04', or tokens like '1d2h30m15s'."
    }

    throw "Cannot convert value of type [$($InputObject.GetType().FullName)] to [TimeSpan]."
}
