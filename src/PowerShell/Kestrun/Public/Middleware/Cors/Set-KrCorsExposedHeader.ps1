<#
.SYNOPSIS
    Set exposed headers for a CORS policy builder.
.DESCRIPTION
    Configures which response headers are exposed to browser JavaScript by adding
    the Access-Control-Expose-Headers list to the CORS policy.
.PARAMETER Builder
    The CorsPolicyBuilder to configure. Accepts from pipeline.
.PARAMETER Headers
    One or more header names to expose to the client.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsExposedHeader -Headers 'X-Total-Count','X-Page-Number'
.OUTPUTS
    Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder
#>
function Set-KrCorsExposedHeader {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(Mandatory)]
        [string[]]$Headers
    )
    process {
        # ASP.NET Core: WithExposedHeaders adds Access-Control-Expose-Headers
        $Builder.WithExposedHeaders($Headers) | Out-Null
        $Builder
    }
}
