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
.EXAMPLE
    # Add contact information to the default document
    Add-KrOpenApiContact -Name "John Doe" -Url "https://johndoe.com" -Email "john.doe@example.com"
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiContact {
    [KestrunRuntimeApi('Everywhere')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string[]]$DocId = @('default'),
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [Uri]$Url,
        [Parameter()]
        [string]$Email
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
            if ($null -eq $docDescriptor.Document.Info.Contact) {
                # Initialize the Contact object if null
                $docDescriptor.Document.Info.Contact = [Microsoft.OpenApi.OpenApiContact]::new()
            }
            if ($PsBoundParameters.ContainsKey('Name')) {
                $docDescriptor.Document.Info.Contact.Name = $Name
            }
            if ($PsBoundParameters.ContainsKey('Url')) {
                $docDescriptor.Document.Info.Contact.Url = $Url
            }
            if ($PsBoundParameters.ContainsKey('Email')) {
                $docDescriptor.Document.Info.Contact.Email = $Email
            }
        }
    }
}
