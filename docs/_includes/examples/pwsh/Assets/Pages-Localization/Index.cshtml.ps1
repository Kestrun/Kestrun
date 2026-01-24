[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
Write-Host "Rendering localized Razor page for culture: $($Context.Culture)"
$culture = [System.Globalization.CultureInfo]::CurrentCulture
$now = Get-Date
$Model = [pscustomobject]@{
    Culture = $Context.Culture
    Title = (Get-KrLocalizedString -Key 'Page.Title' -Default 'Localized Razor Page')
    Message = (Get-KrLocalizedString -Key 'Hello' -Default 'Hello')
    Save = (Get-KrLocalizedString -Key 'Labels.Save' -Default 'Save')
    DateSample = $now.ToString('D', $culture)
    CurrencySample = 1234.56.ToString('C', $culture)
}
