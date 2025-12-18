[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
# These variables EXIST because they were defined
# before Enable-KrConfiguration in the main script

$Model = [pscustomobject]@{
    App = $AppInfo
    Flags = $FeatureFlags
    Motd = $Motd
    Host = (Expand-KrObject -InputObject $KrServer -PassThru)
}
