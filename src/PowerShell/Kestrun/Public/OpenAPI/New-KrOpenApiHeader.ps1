<#
.SYNOPSIS
    Creates a new OpenAPI Header object.
.DESCRIPTION
    This cmdlet creates a new OpenAPI Header object that can be used in OpenAPI specifications.
.PARAMETER Server
    The Kestrun server instance to use. If not specified, the default server instance is used.
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
    A single example of the header value.
.PARAMETER Examples
    A dictionary of multiple examples for the header.
.PARAMETER Schema
    The schema defining the type of the header value. This can be a .NET type literal (e.g., [string], [int], etc.).
.PARAMETER Content
    A dictionary representing the content of the header, mapping media types to OpenAPI MediaType objects.
.PARAMETER Extensions
    A dictionary of OpenAPI extensions to add to the header.
.OUTPUTS
    Microsoft.OpenApi.OpenApiHeader object.
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
        [System.Collections.IDictionary]$Content,

        [Parameter()]
        [System.Collections.IDictionary]$Extensions
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($PSCmdlet.ParameterSetName -eq 'Schema' -and $null -ne $Schema) {
            $Schema = Resolve-KrSchemaTypeLiteral -Schema $Schema
        }
    }
    process {
        # Create header for the specified OpenAPI document
        if ($Server.OpenApiDocumentDescriptor.Count -gt 0 ) {
            $docDescriptor = $Server.DefaultOpenApiDocumentDescriptor
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
                $Content,
                $Extensions
            )
            return $header
        }
    }
}
