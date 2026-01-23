<#
    .SYNOPSIS
        Returns a localized string for the current request culture.
    .DESCRIPTION
        Looks up a key in the request-local string table exposed by Kestrun localization middleware.
        The current culture is determined by middleware and is not passed explicitly.
    .PARAMETER Key
        The localization key to retrieve.
    .PARAMETER Default
        Optional default value to return when the key is not present.
    .EXAMPLE
        Get-KrString -Key 'Labels.Save'
    .EXAMPLE
        Get-KrString -Key 'Labels.Save' -Default 'Save'
    .OUTPUTS
        System.String
#>
function Get-KrString {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Key,

        [Parameter()]
        [string]$Default
    )

    if ($null -eq $Context -or $null -eq $Context.Strings) {
        if ($PSBoundParameters.ContainsKey('Default')) {
            return $Default
        }
        return $null
    }

    if ($Context.Strings.ContainsKey($Key)) {
        return $Context.Strings[$Key]
    }

    if ($PSBoundParameters.ContainsKey('Default')) {
        return $Default
    }

    return $null
}
