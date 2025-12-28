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
    The URL of the license. This parameter is used in the 'WithUrl' parameter set and is mutually exclusive with the 'Identifier' parameter.
.PARAMETER Identifier
    The SPDX identifier of the license. This parameter is used in the 'WithIdentifier' parameter set and is mutually exclusive with the 'Url' parameter.
.EXAMPLE
    # Add license information to the default document
    Add-KrOpenApiLicense -Name 'MIT License' -Url 'https://opensource.org/licenses/MIT'
.EXAMPLE
    # Add license information using SPDX identifier to the default document
    Add-KrOpenApiLicense -Name 'Apache 2.0' -Identifier 'Apache-2.0'
.EXAMPLE
    # Add license information using URL to the default document
    Add-KrOpenApiLicense -Name 'GPLv3' -Url 'https://www.gnu.org/licenses/gpl-3.0.en.html' -DocId 'customDoc1','customDoc2'
.NOTES
    This cmdlet is part of the OpenAPI module.
#>
function Add-KrOpenApiLicense {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'WithUrl')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'WithUrl')]
        [Uri]$Url,

        [Parameter(Mandatory = $true, ParameterSetName = 'WithIdentifier')]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern('^(?!\s)(?!.*\s$)(?!.*:\/\/)(?:\(?\s*[A-Za-z0-9.-]+[+]?(?:\s+WITH\s+[A-Za-z0-9.-]+)?\s*\)?)(?:\s+(?:AND|OR)\s+(?:\(?\s*[A-Za-z0-9.-]+[+]?(?:\s+WITH\s+[A-Za-z0-9.-]+)?\s*\)?))*$')]
        [string]$Identifier
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        # SPDX-ish license expression regex:
        # - allows LICENSEID, LICENSEID+, ( ... ), AND/OR, and "WITH exception"
        # - avoids URLs
        $script:SpdxExpressionRegex = '^(?!.*:\/\/)(?:\(?\s*[A-Za-z0-9.-]+[+]?(?:\s+WITH\s+[A-Za-z0-9.-]+)?\s*\)?)(?:\s+(?:AND|OR)\s+(?:\(?\s*[A-Za-z0-9.-]+[+]?(?:\s+WITH\s+[A-Za-z0-9.-]+)?\s*\)?))*$'
    }
    process {

        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)

            # Initialize the Info object if null
            $docDescriptor.Document.Info ??= [Microsoft.OpenApi.OpenApiInfo]::new()

            # Initialize the License object if null
            $docDescriptor.Document.Info.License ??= [Microsoft.OpenApi.OpenApiLicense]::new()

            # Set the license information
            $docDescriptor.Document.Info.License.Name = $Name
            # Set optional properties if provided
            if ($PSCmdlet.ParameterSetName -eq 'WithUrl') {
                $docDescriptor.Document.Info.License.Url = $Url
            }

            if ($PSCmdlet.ParameterSetName -eq 'WithIdentifier') {
                # Assign the SPDX identifier
                $docDescriptor.Document.Info.License.Identifier = $Identifier
            }
        }
    }
}
