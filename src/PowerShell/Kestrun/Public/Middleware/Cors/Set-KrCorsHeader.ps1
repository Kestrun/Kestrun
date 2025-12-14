<#
.SYNOPSIS
    Set CORS headers for a given CORS policy builder.
.DESCRIPTION
    This function sets CORS headers for a given CORS policy builder. It supports setting any header or specific headers based on the provided parameters.
.PARAMETER Builder
    The CORS policy builder to set the headers for. This parameter is mandatory and must be provided.
.PARAMETER Any
    A switch parameter to allow any header. If this parameter is provided, the function will set the CORS policy to allow any header.
.PARAMETER Headers
    An array of strings representing the specific headers to allow. This parameter is mandatory when the 'Any' switch parameter is not provided.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsHeader -Any
    This example sets the CORS policy to allow any header.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsHeader -Headers @('Content-Type', 'Authorization')
    This example sets the CORS policy to allow specific headers ('Content-Type' and 'Authorization').
.OUTPUTS
    Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder
.NOTES
    This function is part of the Kestrun PowerShell module and is used to configure CORS policies in
#>
function Set-KrCorsHeader {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(DefaultParameterSetName = 'With')]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(Mandatory, ParameterSetName = 'Any')]
        [switch]$Any,

        [Parameter(Mandatory, ParameterSetName = 'With')]
        [string[]]$Headers
    )
    process {
        if ($Any) { $Builder.AllowAnyHeader() | Out-Null }
        else { $Builder.WithHeaders($Headers) | Out-Null }
        $Builder
    }
}
