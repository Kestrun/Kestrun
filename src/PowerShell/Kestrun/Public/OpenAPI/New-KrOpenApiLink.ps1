<#
.SYNOPSIS
    Creates a new OpenAPI Link object.
.DESCRIPTION
    This function creates a new OpenAPI Link object that can be used to define relationships between operations
    in an OpenAPI specification. Links allow you to specify how the output of one operation can be used as input to another operation.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI link will be associated.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER OperationRef
    A reference to an existing operation in the OpenAPI specification using a JSON Reference.
.PARAMETER OperationId
    The operationId of an existing operation in the OpenAPI specification.
.PARAMETER Description
    A description of the link.
.PARAMETER OpenApiServer
    An OpenAPI Server object that specifies the server to be used for the linked operation.
.PARAMETER Parameters
    A hashtable mapping parameter names to runtime expressions or literal objects that define the parameters for the linked operation.
.PARAMETER RequestBody
    A runtime expression or literal object that defines the request body for the linked operation.
.PARAMETER Extensions
    A collection of OpenAPI extensions to add to the link.
.EXAMPLE
    $link = New-KrOpenApiLink -OperationId "getUser" -Description "Link to get user details" -Parameters @{ "userId" = "$response.body#/id" }
    This link creates a new OpenAPI Link object that links to the "getUser" operation, with a description and parameters.
.EXAMPLE
    $link = New-KrOpenApiLink -OperationRef "#/paths/~1users~1{userId}/get" -Description "Link to get user details"
    This link creates a new OpenAPI Link object that links to the operation referenced by the provided JSON Reference, with a description.
.OUTPUTS
    Microsoft.OpenApi.OpenApiLink object.
.NOTES
    This function is part of the Kestrun PowerShell module for working with OpenAPI specifications.
#>
function New-KrOpenApiLink {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    [OutputType([Microsoft.OpenApi.OpenApiLink])]
    [CmdletBinding(DefaultParameterSetName = 'ByOperationId')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByOperationRef')]
        [ValidateNotNullOrEmpty()]
        [string]$OperationRef,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByOperationId')]
        [ValidateNotNullOrEmpty()]
        [string]$OperationId,

        [Parameter()]
        [string]$Description,

        [Parameter()]
        [Microsoft.OpenApi.OpenApiServer]$OpenApiServer,

        [Parameter()]
        [System.Collections.IDictionary]$Parameters,

        [Parameter()]
        [object] $RequestBody,

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
            $link = $docDescriptor.NewOpenApiLink(
                $OperationRef,
                $OperationId,
                $Description,
                $OpenApiServer,
                $Parameters,
                $RequestBody,
                $Extensions)
            return $link
        }
    }
}
