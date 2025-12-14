<#
.SYNOPSIS
    Set the CORS credentials policy for a given CorsPolicyBuilder object.
.DESCRIPTION
    This function sets the CORS credentials policy for a given CorsPolicyBuilder object.
    It can either allow or disallow credentials based on the provided parameters.
    The function uses the CorsPolicyBuilder object to configure the CORS policy.
    The function can be used in a pipeline to configure the CORS policy for a given CorsPolicy
    object.
.PARAMETER Builder
    The CorsPolicyBuilder object to configure.
.PARAMETER Allow
    Allows credentials in the CORS policy.
.PARAMETER Disallow
    Disallows credentials in the CORS policy.
.EXAMPLE
    $corsBuilder = New-KrCorsPolicyBuilder  | Set-KrCorsCredential -Allow
.EXAMPLE
    $corsBuilder = New-KrCorsPolicyBuilder  | Set-KrCorsCredential -Disallow
.OUTPUTS
    Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder
#>
function Set-KrCorsCredential {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Allow')]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(ParameterSetName = 'Allow')]
        [switch]$Allow,

        [Parameter(ParameterSetName = 'Disallow')]
        [switch]$Disallow
    )
    process {
        if ($Allow -and $Disallow) { throw 'Choose -Allow or -Disallow, not both.' }

        if ($Allow) {
            # This will throw later at runtime too, but it’s nicer to catch early:
            # AllowCredentials + AllowAnyOrigin is invalid.
            $Builder.AllowCredentials() | Out-Null
        }

        if ($Disallow) {
            # This will throw later at runtime too, but it’s nicer to catch early:
            # DisallowCredentials + AllowAnyOrigin is invalid.
            $Builder.DisallowCredentials() | Out-Null
        }

        return $Builder
    }
}
