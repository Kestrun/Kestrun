<#
.SYNOPSIS
    Adds a tag to the OpenAPI document.
.DESCRIPTION
    This function adds a tag to the OpenAPI document using the provided parameters in the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI tag will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the tag will be added. Default is 'default'.
.PARAMETER Name
    The name of the tag.
.PARAMETER Description
    A description of the tag.
.PARAMETER ExternalDocs
    An OpenAPI External Documentation object associated with the tag.
.EXAMPLE
    # Add a tag to the default document
    Add-KrOpenApiTag -Name 'MyTag' -Description 'This is my tag.' `
        -ExternalDocs (New-KrOpenApiExternalDoc -Description 'More info' -Url 'https://example.com/tag-info')
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiTag {
    [KestrunRuntimeApi('Everywhere')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string[]]$DocId = @('default'),
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [string]$Description,
        [Parameter()]
        [Microsoft.OpenApi.OpenApiExternalDocs]$ExternalDocs
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            if ($null -eq $docDescriptor.Document.Tags) {
                # Initialize the Tags collection if null
                $docDescriptor.Document.Tags = [System.Collections.Generic.HashSet[Microsoft.OpenApi.OpenApiTag]]::new()
            }
            $tag = [Microsoft.OpenApi.OpenApiTag]::new()
            $tag.Name = $Name
            if ($PsBoundParameters.ContainsKey('Description')) {
                $tag.Description = $Description
            }
            if ($PsBoundParameters.ContainsKey('ExternalDocs')) {
                $tag.ExternalDocs = $ExternalDocs
            }
            $docDescriptor.Document.Tags.Add($tag)
        }
    }
}
