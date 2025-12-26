<#
.SYNOPSIS
    Adds or updates the OpenAPI Info section in the specified OpenAPI documents.
.DESCRIPTION
    This function adds or updates the OpenAPI Info section using the provided parameters in the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI Info will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the Info will be added. Default is 'default'.
.PARAMETER Title
    The title of the API.
.PARAMETER Version
    The version of the API.
.PARAMETER Summary
    A short summary of the API.
.PARAMETER Description
    A detailed description of the API.
.PARAMETER TermsOfService
    A URI to the Terms of Service for the API.
.EXAMPLE
    # Add or update the OpenAPI Info section in the default document
    Add-KrOpenApiInfo -Title 'My API' -Version '1.0.0' -Description 'This is my API.' `
        -Summary 'A brief summary of my API.' -TermsOfService 'https://example.com/terms'
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiInfo {
    [KestrunRuntimeApi('Everywhere')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,
        [Parameter(Mandatory)]
        [string]$Title,
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter()]
        [string]$Summary,
        [Parameter()]
        [string]$Description,
        [Parameter()]
        [uri]$TermsOfService
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            if ($null -eq $docDescriptor.Document.Info) {
                # Initialize the Info object if null
                $docDescriptor.Document.Info = [Microsoft.OpenApi.OpenApiInfo]::new()
            }
            if ($PsBoundParameters.ContainsKey('Title')) {
                $docDescriptor.Document.Info.Title = $Title
            }
            if ($PsBoundParameters.ContainsKey('Version')) {
                $docDescriptor.Document.Info.Version = $Version
            }
            if ($PsBoundParameters.ContainsKey('Summary')) {
                $docDescriptor.Document.Info.Summary = $Summary
            }
            if ($PsBoundParameters.ContainsKey('Description')) {
                $docDescriptor.Document.Info.Description = $Description
            }
            if ($PsBoundParameters.ContainsKey('TermsOfService')) {
                $docDescriptor.Document.Info.TermsOfService = $TermsOfService
            }
        }
    }
}
