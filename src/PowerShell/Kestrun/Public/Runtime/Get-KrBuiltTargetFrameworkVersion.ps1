<#
.SYNOPSIS
    Gets the target framework version that Kestrun was built against.
.DESCRIPTION
    This cmdlet retrieves the target framework version that the Kestrun runtime was built against.
    This information can be useful for understanding the capabilities and features available in the current Kestrun runtime environment.
.OUTPUTS
    System.Version
.EXAMPLE
    Get-KrBuiltTargetFrameworkVersion
    This example retrieves the target framework version that Kestrun was built against.
    The output will be a Version object representing the target framework version.
#>
function Get-KrBuiltTargetFrameworkVersion {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Version])]
    param()
    [Kestrun.KestrunRuntimeInfo]::GetBuiltTargetFrameworkVersion()
}
