<#
.SYNOPSIS
    Adds contact information to the OpenAPI document.
.DESCRIPTION
    This function adds contact information to the OpenAPI Info section using the provided parameters in the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI contact information will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the contact information will be added. Default is 'default'.
.PARAMETER Name
    The name of the contact person/organization.
.PARAMETER Url
    The URL of the contact person/organization.
.PARAMETER Email
    The email address of the contact person/organization.
.PARAMETER Extensions
    A collection of OpenAPI extensions to add to the contact information.
.EXAMPLE
    # Add contact information to the default document
    Add-KrOpenApiContact -Name "John Doe" -Url "https://johndoe.com" -Email "john.doe@example.com"
    Adds contact information with the specified name, URL, and email to the default OpenAPI document.
.EXAMPLE
    # Add contact information to multiple documents
    Add-KrOpenApiContact -DocId @('Default', 'v2') -Name "API Support" -Email "support@example.com"
    Adds contact information with the specified name and email to both the 'Default' and 'v2' OpenAPI documents.
.EXAMPLE
    # Add contact information with extensions
    $extensions = [ordered]@{
        'x-contact-type' = 'technical'
        'x-timezone' = 'PST'
    }
    Add-KrOpenApiContact -Name "Tech Support" -Email "techsupport@example.com" -Extensions $extensions
    Adds contact information with the specified name, email, and extensions to the default OpenAPI document.
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiContact {
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter()]
        [string]$Name,

        [Parameter()]
        [Uri]$Url,

        [Parameter()]
        [string]$Email,

        [Parameter()]
        [System.Collections.IDictionary]$Extensions = $null
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
            $docDescriptor.Document.Info.Contact = $docDescriptor.CreateInfoContact($Name, $Url, $Email, $Extensions)
        }
    }
}
