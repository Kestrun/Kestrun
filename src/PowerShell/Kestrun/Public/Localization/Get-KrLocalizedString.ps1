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
        Get-KrLocalizedString -Key 'Labels.Save'
    .EXAMPLE
        Get-KrLocalizedString -Key 'Labels.Save' -Default 'Save'
    .OUTPUTS
        System.String
#>
function Get-KrLocalizedString {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Key,

        [Parameter()]
        [string]$Default
    )

    $strings = $null
    if ($null -ne $Context) {
        $localizedProp = $Context.PSObject.Properties['LocalizedStrings']
        if ($null -ne $localizedProp) {
            $strings = $localizedProp.Value
        } elseif ($Context.PSObject.Properties['Strings']) {
            $strings = $Context.Strings
        }
    }

    if ($null -eq $strings) {
        if ($PSBoundParameters.ContainsKey('Default')) {
            return $Default
        }
        return $null
    }

    if ($strings.ContainsKey($Key)) {
        return $strings[$Key]
    }

    if ($PSBoundParameters.ContainsKey('Default')) {
        return $Default
    }

    return $null
}
