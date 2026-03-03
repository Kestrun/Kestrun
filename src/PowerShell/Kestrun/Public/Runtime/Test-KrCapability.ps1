<#
.SYNOPSIS
    Check if a specific feature is supported in the current Kestrun runtime environment.
.DESCRIPTION
    This cmdlet checks if a given feature, identified by its name, is supported in the current Kestrun runtime environment.
    It can be used to determine if certain capabilities are available based on the runtime version and configuration.
    For HTTP/3 checks, this cmdlet also verifies platform QUIC availability.
.PARAMETER Feature
    The name of the feature to check. This can be either the name of a KnownFeature enum value or a raw string representing the feature.
.EXAMPLE
    Test-KrCapability -Feature "Http3"
    This example checks if HTTP/3 is supported and QUIC is available on the current platform/runtime.
.EXAMPLE
    Test-KrCapability -Feature "Quic"
    This example checks if QUIC is available using Kestrun host capability detection.
.EXAMPLE
    Test-KrCapability -Feature "SomeOtherFeature"
    This example checks if a feature named "SomeOtherFeature" is supported, using a raw string.
#>
function Test-KrCapability {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Feature
    )

    # QUIC/HTTP3 capability requires both runtime feature support and platform QUIC availability
    if ($Feature -in @('Http3', 'Quic', 'QuicSupport')) {
        return [Kestrun.KestrunRuntimeInfo]::Supports('Http3') -and [Kestrun.Hosting.KestrunHost]::IsQuicSupported()
    }

    # Allow either enum name or raw string
    try {
        $enum = [Kestrun.KestrunRuntimeInfo+KnownFeature]::$Feature
        return [Kestrun.KestrunRuntimeInfo]::Supports($enum)
    } catch [System.ArgumentException] {
        return [Kestrun.KestrunRuntimeInfo]::Supports($Feature)
    }
}
