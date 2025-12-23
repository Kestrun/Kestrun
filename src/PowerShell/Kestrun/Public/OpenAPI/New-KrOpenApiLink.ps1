<#
.SYNOPSIS
    Creates a new OpenAPI Link object.
.DESCRIPTION
    This function creates a new OpenAPI Link object that can be used to define relationships between operations
    in an OpenAPI specification. Links allow you to specify how the output of one operation can be used as input to another operation.
.PARAMETER OperationRef
    A reference to an existing operation in the OpenAPI specification using a JSON Reference.
.PARAMETER OperationId
    The operationId of an existing operation in the OpenAPI specification.
.PARAMETER Description
    A description of the link.
.PARAMETER Server
    An OpenAPI Server object that specifies the server to be used for the linked operation.
.PARAMETER Parameters
    A hashtable mapping parameter names to runtime expressions or literal objects that define the parameters for the linked operation.
.PARAMETER RequestBody
    A runtime expression or literal object that defines the request body for the linked operation.
.EXAMPLE
    $link = New-KrOpenApiLink -OperationId "getUser" -Description "Link to get user details" -Parameters @{ "userId" = "$response.body#/id" }
    This link creates a new OpenAPI Link object that links to the "getUser" operation, with a description and parameters.
.NOTES
    This function is part of the Kestrun PowerShell module for working with OpenAPI specifications.
#>
function New-KrOpenApiLink {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'ByOperationId')]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'ByOperationRef')]
        [ValidateNotNullOrEmpty()]
        [string]$OperationRef,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByOperationId')]
        [ValidateNotNullOrEmpty()]
        [string]$OperationId,

        [string]$Description,

        # Accept a prebuilt OpenAPI server (use New-KrOpenApiServer)
        [Microsoft.OpenApi.OpenApiServer] $Server,

        # Hashtable: name -> string(runtime expression) OR literal object
        [hashtable]$Parameters,

        # ✅ Support string(runtime expression) OR literal object (hashtable/array/etc.)
        [object] $RequestBody
    )

    # Extra safety: parameter sets should prevent this, but keep it robust.
    if (-not [string]::IsNullOrWhiteSpace($OperationId) -and -not [string]::IsNullOrWhiteSpace($OperationRef)) {
        throw "OperationId and OperationRef are mutually exclusive in an OpenAPI Link."
    }

    $link = [Microsoft.OpenApi.OpenApiLink]::new()

    if (-not [string]::IsNullOrWhiteSpace($Description)) {
        $link.Description = $Description
    }

    if ($null -ne $Server) {
        $link.Server = $Server
    }

    switch ($PSCmdlet.ParameterSetName) {
        'ByOperationRef' { $link.OperationRef = $OperationRef }
        'ByOperationId'  { $link.OperationId  = $OperationId  }
    }

    # RequestBody: runtime expression string OR literal object
    if ($null -ne $RequestBody) {
        $rbWrapper = [Microsoft.OpenApi.RuntimeExpressionAnyWrapper]::new()

        if ($RequestBody -is [string] -and -not [string]::IsNullOrWhiteSpace($RequestBody)) {
            $rbWrapper.Any = [Microsoft.OpenApi.RuntimeExpression]::Build([string]$RequestBody)
            $link.RequestBody = $rbWrapper
        }
        elseif ($RequestBody -isnot [string]) {
            $rbWrapper.Any = [Kestrun.OpenApi.OpenApiJsonNodeFactory]::FromObject($RequestBody)
            $link.RequestBody = $rbWrapper
        }
    }

    # Parameters
    if ($null -ne $Parameters -and $Parameters.Count -gt 0) {
        $link.Parameters ??= [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.RuntimeExpressionAnyWrapper]]::new()

        foreach ($key in $Parameters.Keys) {
            $value = $Parameters[$key]
            $pWrapper = [Microsoft.OpenApi.RuntimeExpressionAnyWrapper]::new()

            if ($value -is [string]) {
                $pWrapper.Any = [Microsoft.OpenApi.RuntimeExpression]::Build([string]$value)
            } else {
                $pWrapper.Any = [Kestrun.OpenApi.OpenApiJsonNodeFactory]::FromObject($value)
            }

            $link.Parameters[$key] = $pWrapper
        }
    }

    return $link
}

