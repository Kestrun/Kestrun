<#
.SYNOPSIS
    Adds license information to the OpenAPI document.
.DESCRIPTION
    This function adds license information to the OpenAPI Info section using the provided parameters in the specified OpenAPI documents in the Kestrun server.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI license information will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    An array of OpenAPI document IDs to which the license information will be added. Default is 'default'.
.PARAMETER Name
    The name of the license.
.PARAMETER Url
    The URL of the license.
.PARAMETER Identifier
    The SPDX identifier of the license.
.EXAMPLE
    # Add license information to the default document
    Add-KrOpenApiLicense -Name 'MIT License' -Url 'https://opensource.org/licenses/MIT' -Identifier 'MIT'
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiLicense {
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
        [string]$Identifier
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
            if ($null -eq $docDescriptor.Document.Info.License) {
                # Initialize the License object if null
                $docDescriptor.Document.Info.License = [Microsoft.OpenApi.OpenApiLicense]::new()
            }
            if ($PsBoundParameters.ContainsKey('Name')) {
                $docDescriptor.Document.Info.License.Name = $Name
            }
            if ($PsBoundParameters.ContainsKey('Url')) {
                $docDescriptor.Document.Info.License.Url = $Url
            }
            if ($PsBoundParameters.ContainsKey('Identifier')) {
                $docDescriptor.Document.Info.License.Identifier = $Identifier
            }
        }
    }
}
