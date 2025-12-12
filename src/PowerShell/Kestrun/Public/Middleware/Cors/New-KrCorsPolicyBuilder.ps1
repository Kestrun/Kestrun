<#
.SYNOPSIS
    Creates a new CORS policy builder.
.DESCRIPTION
    This function creates a new CORS policy builder, which can be used to configure CORS (Cross-Origin Resource Sharing) policies in an ASP.NET Core application.
.EXAMPLE
    $corsBuilder = New-KrCorsPolicyBuilder
    $corsBuilder =WithOrigins("https://example.com") |WithMethods("("Get") |WithHeaders("("-Control-Allow-Origin")]
.OUTPUTS
    Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder
#>
function New-KrCorsPolicyBuilder {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param()
    return [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]::new()
}
