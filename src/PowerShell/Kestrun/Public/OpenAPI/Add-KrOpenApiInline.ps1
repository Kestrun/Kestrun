<#
.SYNOPSIS
    Adds an inline OpenAPI element (Example or Link) to the specified OpenAPI document(s).
.DESCRIPTION
    This cmdlet adds an inline OpenAPI element, either an Example or a Link, to one or more OpenAPI documents managed by the Kestrun server.
.PARAMETER Server
    The Kestrun server instance where the OpenAPI documents are managed. If not specified, the default server instance is used.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the element will be added. Defaults to the standard documentation IDs.
.PARAMETER Name
    The name of the inline element to be added. This can be provided via the pipeline or as a parameter.
.PARAMETER Element
    The OpenAPI inline element object to be added. This can be an OpenApiExample or OpenApiLink object.
.PARAMETER IfExists
    Specifies the conflict resolution strategy if an element with the same name already exists in the document. Options are Overwrite, Ignore, or Error. Defaults to Overwrite.
.EXAMPLE
    $example = New-KrOpenApiComponentExample -Summary "User Example" -Description "An example of a user object." -Value @{ id = 1; name = "John Doe" }
    Add-KrOpenApiInline -Name "UserExample" -Element $example -DocId "MyApiDoc"
    This example creates a new OpenAPI Inline Example and adds it to the "MyApiDoc" OpenAPI document.
.EXAMPLE
    $link = New-KrOpenApiLink -OperationId "getUser" -Description "Link to get user details" -Parameters @{ "userId" = "$response.body#/id" }
    Add-KrOpenApiInline -Name "GetUserLink" -Element $link -DocId "MyApiDoc"
    This example creates a new OpenAPI Inline Link and adds it to the "MyApiDoc" OpenAPI document.
#>
function Add-KrOpenApiInline {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false )]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultDocumentationIds,

        [Parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [Alias('Key', 'Id')]
        [ValidatePattern('^[A-Za-z0-9\.\-_]+$')]
        [string] $Name,

        [Parameter(Mandatory = $true, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [object] $Element,

        [Parameter()]
        [Kestrun.OpenApi.OpenApiComponentConflictResolution] $IfExists = [Kestrun.OpenApi.OpenApiComponentConflictResolution]::Overwrite
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)

            if ($Element -is [Microsoft.OpenApi.OpenApiExample]) {
                $docDescriptor.AddComponentExample($Name, $Element, $IfExists)
            } elseif ($Element -is [Microsoft.OpenApi.OpenApiLink]) {
                $docDescriptor.AddComponentLink($Name, $Element, $IfExists)
            } else {
                throw [System.ArgumentException]::new(
                    "Unsupported inline element type: $($Element.GetType().FullName). Supported: OpenApiExample, OpenApiLink.",
                    'Element'
                )
            }
        }
    }
}
