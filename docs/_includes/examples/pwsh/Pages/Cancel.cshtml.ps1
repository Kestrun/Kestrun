[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

$Model = [pscustomobject]@{
    HasAntiforgery = ($null -ne $KrServer.AntiforgeryOptions)
    Seconds        = 30
}
