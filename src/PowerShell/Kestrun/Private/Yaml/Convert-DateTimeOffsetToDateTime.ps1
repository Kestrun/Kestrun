<#
.SYNOPSIS
    Recursively converts DateTimeOffset instances to DateTime in a given object.
.DESCRIPTION
    This function takes an input object and recursively traverses its structure.
    If it encounters any DateTimeOffset instances, it converts them to DateTime.
    It handles nested dictionaries and lists, ensuring that all DateTimeOffset
    instances are converted regardless of their depth in the object hierarchy.
.PARAMETER InputObject
    The object to process, which may contain DateTimeOffset instances.
.EXAMPLE
    $obj = @{
        Date1 = [DateTimeOffset]::Now
        Nested = @{ Date2 = [DateTimeOffset]::UtcNow }
        List = @([DateTimeOffset]::Now, "string", 123)
    }
    $converted = Convert-DateTimeOffsetToDateTime $obj
    # $converted will have DateTime instances instead of DateTimeOffset.
.NOTES
    This function is useful for ensuring compatibility with systems that expect DateTime
    rather than DateTimeOffset, such as certain serialization scenarios.
#>
function Convert-DateTimeOffsetToDateTime {
    param(
        [Parameter()]
        [AllowNull()]
        $InputObject
    )
    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [DateTimeOffset]) {
        # Preserve naive timestamps (those without explicit zone) as 'Unspecified' kind so no offset is appended
        $dto = [DateTimeOffset]$InputObject
        return [DateTime]::SpecifyKind($dto.DateTime, [System.DateTimeKind]::Unspecified)
    }
    if ($InputObject -is [System.Collections.IDictionary]) {
        foreach ($k in @($InputObject.Keys)) { $InputObject[$k] = Convert-DateTimeOffsetToDateTime $InputObject[$k] }
        return $InputObject
    }
    if ($InputObject -is [System.Collections.IList]) {
        for ($j = 0; $j -lt $InputObject.Count; $j++) { $InputObject[$j] = Convert-DateTimeOffsetToDateTime $InputObject[$j] }
        return $InputObject
    }
    return $InputObject
}
