<#
.SYNOPSIS
    Check if a specific feature is supported in the current Kestrun runtime environment.
.DESCRIPTION
    This cmdlet checks if a given feature, identified by its name, is supported in the current Kestrun runtime environment.
    It can be used to determine if certain capabilities are available based on the runtime version and configuration.
.PARAMETER Feature
    The name of the feature to check. This can be either the name of a KnownFeature enum value or a raw string representing the feature.
.EXAMPLE
    Test-KrCapability -Feature "Http3"
    This example checks if the Http3 feature is supported in the current Kestrun runtime environment.
.EXAMPLE
    Test-KrCapability -Feature "SomeOtherFeature"
    This example checks if a feature named "SomeOtherFeature" is supported, using a raw string.
#>
function Test-KrCapability {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Feature
    )
    # Allow either enum name or raw string
    try {
        $enum = [Kestrun.KestrunRuntimeInfo+KnownFeature]::$Feature
        return [Kestrun.KestrunRuntimeInfo]::Supports($enum)
    } catch [System.ArgumentException] {
        return [Kestrun.KestrunRuntimeInfo]::Supports($Feature)
    }
}
