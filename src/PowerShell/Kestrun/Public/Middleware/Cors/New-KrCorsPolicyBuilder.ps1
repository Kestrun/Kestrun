<#
.SYNOPSIS
    Creates a new CORS policy builder.
.DESCRIPTION
    This function creates a new CORS policy builder, which can be used to configure CORS (Cross-Origin Resource Sharing) policies in an ASP.NET Core application.
.EXAMPLE
     New-KrCorsPolicyBuilder | Set-KrCorsMethod -Any | Set-KrCorsHeader -Any | Add-KrCorsPolicy -Server $server -Name 'MyCORSPolicy'
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
