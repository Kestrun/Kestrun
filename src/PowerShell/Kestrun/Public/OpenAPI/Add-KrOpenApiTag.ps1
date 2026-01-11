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
.PARAMETER Summary
    A short summary of the tag.
.PARAMETER Description
    A description of the tag.
.PARAMETER Parent
    The name of the parent tag, if this tag is a sub-tag.
.PARAMETER Kind
    A machine-readable string to categorize what sort of tag it is.
.PARAMETER ExternalDocs
    An OpenAPI External Documentation object associated with the tag.
.PARAMETER Extensions
    A collection of OpenAPI extensions to add to the tag.
.EXAMPLE
    # Add a tag to the default document
    Add-KrOpenApiTag -Name 'MyTag' -Description 'This is my tag.' `
        -ExternalDocs (New-KrOpenApiExternalDoc -Description 'More info' -Url 'https://example.com/tag-info')
    Adds a tag named 'MyTag' with a description and external documentation link to the default OpenAPI document.
.EXAMPLE
    # Add a tag to multiple documents
    Add-KrOpenApiTag -DocId @('Default', 'v2') -Name 'MultiDocTag' -Summary 'Tag for multiple docs'
    Adds a tag named 'MultiDocTag' with a summary to both the 'Default' and 'v2' OpenAPI documents.
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiTag {
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter()]
        [string]$Summary,

        [Parameter()]
        [string]$Description,

        [Parameter()]
        [string]$Parent,

        [Parameter()]
        [string]$Kind,

        [Parameter()]
        [Microsoft.OpenApi.OpenApiExternalDocs]$ExternalDocs,

        [Parameter()]
        [System.Collections.Specialized.OrderedDictionary]$Extensions
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)

            # Add the tag to the document
            $null = $docDescriptor.AddTag($Name, $Description, $Summary, $Parent, $Kind, $ExternalDocs, $Extensions) | Out-Null
        }
    }
}
