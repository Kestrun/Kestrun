<#
.SYNOPSIS
    Creates a new OpenAPI Header object.
.DESCRIPTION
    This cmdlet creates a new OpenAPI Header object that can be used in OpenAPI specifications.
.PARAMETER Server
    The Kestrun server instance to use.
.PARAMETER DocId
    The documentation IDs to which this header will be associated.
.PARAMETER Description
    A brief description of the header.
.PARAMETER Required
    Indicates whether the header is required.
.PARAMETER Deprecated
    Indicates whether the header is deprecated.
.PARAMETER AllowEmptyValue
    Indicates whether the header allows empty values.
.PARAMETER Explode
    Indicates whether the header should be exploded.
.PARAMETER AllowReserved
    Indicates whether the header allows reserved characters.
.PARAMETER Style
    The style of the header (e.g., simple, matrix, label, etc.).
.PARAMETER Example
    An example value for the header.
.PARAMETER Examples
    A hashtable of example values for the header.
.PARAMETER Schema
    The schema type for the header. (Parameter set 'Schema')
.PARAMETER Content
    A hashtable of content media types for the header. (OpenAPi3.2 and above) (Parameter set 'Content')
.EXAMPLE
    $header = New-KrOpenApiHeader -Description "Custom Header" -Required -Schema [string]
    This example creates a new OpenAPI Header object with a description, marks it as required, and sets its schema to string.
.EXAMPLE
    $content = @{
        "application/json" = New-KrOpenApiMediaType -Schema [hashtable]
    }
    $header = New-KrOpenApiHeader -Description "JSON Header" -Content $content
    This example creates a new OpenAPI Header object with a description and sets its content to application/json with a hashtable schema.
.EXAMPLE
    $examples = @{
        "example1" = New-KrOpenApiExample -Summary "Example 1" -Value "Value1"
        "example2" = New-KrOpenApiExample -Summary "Example 2" -Value "Value2"
    }
    $header = New-KrOpenApiHeader -Description "Header with Examples" -Examples $examples -Schema [string]
    This example creates a new OpenAPI Header object with a description, multiple examples, and sets its schema to string.
.OUTPUTS
    Microsoft.OpenApi.OpenApiHeader object.
.NOTES
    This function is part of the Kestrun PowerShell module for working with OpenAPI specifications.
 #>
function New-KrOpenApiHeader {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingInvokeExpression', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Schema')]
    [OutputType([Microsoft.OpenApi.OpenApiHeader])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter()]
        [string]$Description,

        [Parameter()]
        [switch]$Required,

        [Parameter()]
        [switch]$Deprecated,

        [Parameter()]
        [switch]$AllowEmptyValue,

        [Parameter()]
        [switch]$Explode,

        [Parameter()]
        [switch]$AllowReserved,

        [Parameter()]
        [System.Nullable[Microsoft.OpenApi.ParameterStyle]]$Style = $null,

        [Parameter()]
        [object]$Example,

        [Parameter()]
        [hashtable]$Examples,

        [Parameter(ParameterSetName = 'Schema')]
        [object]$Schema,

        [Parameter(ParameterSetName = 'Content')]
        [hashtable]$Content
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($PSCmdlet.ParameterSetName -eq 'Schema' -and $null -ne $Schema) {
            $Schema = Resolve-KrSchemaTypeLiteral -Schema $Schema
        }
    }
    process {
        # Create a new OpenAPI header for the specified document(s) and return it
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            $header = $docDescriptor.NewOpenApiHeader(
                $Description,
                $Required.IsPresent,
                $Deprecated.IsPresent,
                $AllowEmptyValue.IsPresent,
                $Style,
                $Explode.IsPresent,
                $AllowReserved.IsPresent,
                $Example,
                $Examples,
                $Schema,
                $Content
            )
            return $header
        }
    }
}
