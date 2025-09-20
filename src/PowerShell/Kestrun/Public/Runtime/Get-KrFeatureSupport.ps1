<#
.SYNOPSIS
    Gets the support status of known features in the current Kestrun runtime environment.
.DESCRIPTION
    This cmdlet retrieves the support status of all known features defined in the Kestrun runtime.
    It provides information about whether each feature is supported based on the runtime version and configuration.
.PARAMETER Capabilities
    If specified, only the feature name and support status will be returned, omitting the built TFM.
.OUTPUTS
    A custom object with the following properties:
    - Feature: The name of the feature.
    - BuiltTFM: The target framework version that Kestrun was built against.
    - Supported: A boolean indicating whether the feature is supported in the current runtime environment.
.EXAMPLE
    Get-KrFeatureSupport
    This example retrieves the support status of all known features in the current Kestrun runtime environment.
    The output will be a collection of objects, each representing a feature and its support status.
#>
function Get-KrFeatureSupport {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'Full')]
    [OutputType([pscustomobject])]
    param(
        [Parameter(ParameterSetName = 'Capabilities')]
        [switch] $Capabilities
    )

    $built = [Kestrun.KestrunRuntimeInfo]::GetBuiltTargetFrameworkVersion()
    $features = [enum]::GetNames([Kestrun.KestrunRuntimeInfo+KnownFeature])

    foreach ($name in $features) {
        $enum = [Kestrun.KestrunRuntimeInfo+KnownFeature]::$name
        $supported = [Kestrun.KestrunRuntimeInfo]::Supports($enum)
        if ($Capabilities) {
            [pscustomobject]@{
                Feature = $name
                Supported = $supported
            }
        } else {
            [pscustomobject]@{
                Feature = $name
                BuiltTFM = $built
                Supported = $supported
            }
        }
    }
}
