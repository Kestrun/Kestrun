<#
.SYNOPSIS
    Creates a new OpenAPI server variable.
.DESCRIPTION
    This function creates a new OpenAPI server variable using the provided parameters.
.PARAMETER Default
    The default value for the server variable.
.PARAMETER Enum
    An array of possible values for the server variable.
.PARAMETER Description
    A description of the server variable.
.EXAMPLE
    $variable = New-KrOpenApiServerVariable -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Environment name'
.OUTPUTS
    Microsoft.OpenApi.OpenApiServerVariable
#>
function New-KrOpenApiServerVariable {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    param(
        [string]$Default,
        [string[]]$Enum,
        [string]$Description
    )
    $variable = [Microsoft.OpenApi.OpenApiServerVariable]::new()
    $variable.Default = $Default
    $variable.Enum = $Enum
    $variable.Description = $Description
    return $variable
}
