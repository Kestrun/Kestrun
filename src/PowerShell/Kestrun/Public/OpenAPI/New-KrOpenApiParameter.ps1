<#
.SYNOPSIS
    Creates a new OpenAPI Parameter object.
.DESCRIPTION
    This cmdlet creates a new OpenAPI Parameter object that can be used in OpenAPI specifications.
.PARAMETER Server
    The Kestrun server instance to use.
.PARAMETER DocId
    The documentation IDs to which this parameter will be associated.
.PARAMETER Name
    The name of the parameter.
.PARAMETER In
    The location of the parameter (e.g., query, header, path, cookie).
.PARAMETER Description
    A brief description of the parameter.
.PARAMETER Required
    Indicates whether the parameter is required.
.PARAMETER Deprecated
    Indicates whether the parameter is deprecated.
.PARAMETER AllowEmptyValue
    Indicates whether the parameter allows empty values.
.PARAMETER Style
    The style of the parameter (e.g., simple, matrix, label, etc.). Optional.
.PARAMETER Explode
    Indicates whether the parameter should be exploded.
.PARAMETER AllowReserved
    Indicates whether the parameter allows reserved characters.
.PARAMETER Schema
    The schema type for the parameter. (Parameter set 'Schema')
.PARAMETER Example
    An example value for the parameter.
.PARAMETER Examples
    A hashtable of example values for the parameter.
.PARAMETER Content
    A hashtable of content media types for the parameter. (OpenAPi3.2 and above) (Parameter set 'Content')
.EXAMPLE
    $parameter = New-KrOpenApiParameter -Name "userId" -In "query" -Description "ID of the user" -Required -Schema [string]
    This example creates a new OpenAPI Parameter object with a name, location, description, marks it as required, and sets its schema to string.
.EXAMPLE
    $content = @{
        "application/json" = New-KrOpenApiMediaType -Schema [hashtable]
    }
    $parameter = New-KrOpenApiParameter -Name "data" -In "body" -Description "JSON data" -Content $content
    This example creates a new OpenAPI Parameter object with a name, location, description, and sets its content to application/json with a has

 #>
function New-KrOpenApiParameter {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingInvokeExpression', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Schema')]
    [OutputType([Microsoft.OpenApi.OpenApiParameter])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultDocumentationIds,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [Microsoft.OpenApi.ParameterLocation]$In,

        [Parameter()]
        [string]$Description,

        [Parameter()]
        [switch]$Required,

        [Parameter()]
        [switch]$Deprecated,

        [Parameter()]
        [switch]$AllowEmptyValue,

        [Parameter()]
        [System.Nullable[Microsoft.OpenApi.ParameterStyle]]$Style = $null,

        [Parameter()]
        [switch]$Explode,

        [Parameter()]
        [switch]$AllowReserved,

        [object]$Schema,

        [Parameter()]
        [object]$Example,

        [Parameter()]
        [hashtable]$Examples
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($PSCmdlet.ParameterSetName -eq 'Schema' -and $null -ne $Schema) {
            $Schema = Resolve-KrSchemaTypeLiteral -Schema $Schema
        }
    }
    process {
        # Create a new OpenAPI parameter for the specified document(s) and return it
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            $parameter = $docDescriptor.NewOpenApiParameter(
                $Name,
                $In,
                $Description,
                $Required.IsPresent,
                $Deprecated.IsPresent,
                $AllowEmptyValue.IsPresent,
                $Style,
                $Explode.IsPresent,
                $AllowReserved.IsPresent,
                $Schema,
                $Example,
                $Examples
            )
            return $parameter
        }
    }
}
