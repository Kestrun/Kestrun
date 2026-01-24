<#
.SYNOPSIS
    Retrieves the list of available localization cultures.
.DESCRIPTION
    Scans the localization resources and returns the list of available cultures.
.OUTPUTS
    PSCustomObject with a Name property representing each available culture.
.EXAMPLE
    Get-KrLocalizationCulture
    Retrieves the list of available localization cultures.

#>
function Get-KrLocalizationCulture {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $server = [Kestrun.KestrunHostManager]::Default
    if ($null -ne $server -and $null -ne $server.LocalizationStore) {
        $store = $server.LocalizationStore
        # AvailableCultures is an IReadOnlyCollection<string>
        $store.AvailableCultures | ForEach-Object {
            [PSCustomObject]@{
                Name = $_
            }
        }
        return
    }
}
