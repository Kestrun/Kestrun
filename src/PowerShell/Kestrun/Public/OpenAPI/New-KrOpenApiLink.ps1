
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
    This example creates a new OpenAPI Link object that links to the "getUser" operation, with a description and parameters.
.NOTES
    This function is part of the Kestrun PowerShell module for working with OpenAPI specifications.
#>
function New-KrOpenApiLink {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    param(
        [string]$OperationRef,
        [string]$OperationId,
        [string]$Description,
        # Accept a prebuilt OpenAPI server (use New-KrOpenApiServer)
        [Microsoft.OpenApi.OpenApiServer] $Server,
        # Accept hashtable name -> string (runtime expression) or literal object
        [hashtable]$Parameters,
        # Accept string runtime expression or hashtable/array literal object
        [object] $RequestBody
    )

    return [Kestrun.OpenApi.OpenApiLinkFactory]::Create($OperationRef, $OperationId, $Description, $Server, $Parameters, $RequestBody)
}
