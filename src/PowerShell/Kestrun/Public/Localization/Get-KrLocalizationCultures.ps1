<#
.SYNOPSIS
    Retrieves the list of available localization cultures.
.DESCRIPTION
    Scans the localization resources and returns the list of available cultures.
.OUTPUTS
    PSCustomObject with a Name property representing each available culture.
.EXAMPLE
    Get-KrLocalizationCultures
    Retrieves the list of available localization cultures.

#>
function Get-KrLocalizationCultures {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    # Prefer returning runtime-loaded cultures from the active Kestrun host's localization store.
    try {
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
    } catch {
        # ignore and fall back to filesystem enumeration
    }
}
