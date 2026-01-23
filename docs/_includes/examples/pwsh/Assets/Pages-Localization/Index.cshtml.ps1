[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

$Model = [pscustomobject]@{
    Culture = $Context.Culture
    Title   = (Get-KrString -Key 'Page.Title' -Default 'Localized Razor Page')
    Message = (Get-KrString -Key 'Hello' -Default 'Hello')
    Save    = (Get-KrString -Key 'Labels.Save' -Default 'Save')
}
