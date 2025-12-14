<#
.SYNOPSIS
    Sets the methods for a CORS policy in a .NET Core application.
.DESCRIPTION
    This function sets the methods for a CORS policy in a .NET Core application. It takes a `CorsPolicyBuilder` object and either allows any method or specifies a list of methods.
.PARAMETER Builder
    The `CorsPolicyBuilder` object to configure.
.PARAMETER Any
    If specified, allows any HTTP method in the CORS policy.
.PARAMETER Methods
    A list of HTTP methods to allow in the CORS policy.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsMethod -Any
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsMethod -Methods @('GET', 'POST', 'PUT', 'DELETE')
.OUTPUTS
    Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder
#>
function Set-KrCorsMethod {
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
        [string[]]$Methods
    )
    process {
        if ($Any) { $Builder.AllowAnyMethod() | Out-Null }
        else { $Builder.WithMethods($Methods) | Out-Null }
        return $Builder
    }
}
