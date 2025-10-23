<#
.SYNOPSIS
    Creates a new OpenAPI server variable.
.DESCRIPTION
    This function creates a new OpenAPI server variable using the provided parameters.
.PARAMETER Variables
    An optional OrderedDictionary to accumulate server variables.
.PARAMETER Name
    The name of the server variable.
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
    [Diagnostics.CodeAnalysis.SuppressMessage('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline = $true, Position = 0)]
        [System.Collections.Specialized.OrderedDictionary] $Variables,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter()]
        [string] $Default,
        [Parameter()]
        [string[]] $Enum,
        [Parameter()]
        [string] $Description
    )

    begin {
        $dict = $null
    }

    process {
        # Adopt or create the accumulator dictionary
        if ($PSBoundParameters.ContainsKey('Variables') -and $Variables) {
            if (-not $dict) { $dict = $Variables }
        }
        if (-not $dict) {
            $dict = [ordered]@{}
        }
        $dict[$Name] = [Microsoft.OpenApi.OpenApiServerVariable]::new()
    }
    end {

        if ($PSBoundParameters.ContainsKey('Default')) { $dict[$Name].default = $Default }
        if ($PSBoundParameters.ContainsKey('Description')) { $dict[$Name].description = $Description }
        if ($PSBoundParameters.ContainsKey('Enum') -and $Enum) {
            $dict[$Name].enum = $Enum
        }

        # Always emit an OrderedDictionary
        return $dict
    }
}
