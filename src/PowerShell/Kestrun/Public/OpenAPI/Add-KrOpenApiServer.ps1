<#
.SYNOPSIS
    Creates a new OpenAPI server.
.DESCRIPTION
    This function creates a new OpenAPI server using the provided parameters.
.PARAMETER Server
    The Kestrun server instance to which the OpenAPI server will be added.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER Description
    A description of the server.
.PARAMETER DocId
    The documentation IDs to which this server will be added.
.PARAMETER Url
    The URL of the server.
.PARAMETER Variables
    A dictionary of server variables.
.EXAMPLE
    $variables = @{
        env = New-KrOpenApiServerVariable -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Environment name'
    }
    $oaServer = New-KrOpenApiServer -Description 'My API Server' -Url 'https://api.example.com' -Variables $variables
.NOTES
    This cmdlet is part of the Kestrun OpenAPI module.
#>
function Add-KrOpenApiServer {
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,
        [Parameter(Mandatory)]
        [string]$Url,
        [string]$Description,
        [System.Collections.Specialized.OrderedDictionary]$Variables
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $oaServer = [Microsoft.OpenApi.OpenApiServer]::new()
        if ($PsBoundParameters.ContainsKey('Description')) {
            $oaServer.Description = $Description
        }
        $oaServer.Url = $Url
        if ($PsBoundParameters.ContainsKey('Variables')) {
            $oaServer.Variables = [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.OpenApiServerVariable]]::new()
            foreach ($key in $Variables.Keys) {
                $value = $Variables[$key]
                $oaServer.Variables.Add($key, $value)
            }
        }
        # Add the server to the specified OpenAPI documents
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            if ($null -eq $docDescriptor.Document.Servers) {
                # Initialize the Servers list if null
                $docDescriptor.Document.Servers = [System.Collections.Generic.List[Microsoft.OpenApi.OpenApiServer]]::new()
            }
            # Add the server to the Servers list
            $docDescriptor.Document.Servers.Add($oaServer)
        }
    }
}
