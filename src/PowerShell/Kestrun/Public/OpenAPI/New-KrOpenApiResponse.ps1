
function New-KrOpenApiResponse {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingInvokeExpression', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Schema')]
    [OutputType([Microsoft.OpenApi.OpenApiResponse])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string[]]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultDocumentationIds,

        [Parameter()]
        [string]$Summary,

        [Parameter()]
        [string]$Description,

        [Parameter()]
        [hashtable]$Headers,

        [Parameter()]
        [hashtable]$Links,

        [Parameter()]
        [hashtable]$Content
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($PSCmdlet.ParameterSetName -eq 'Schema' -and $null -ne $Schema) {
            $Schema = Resolve-KrSchemaTypeLiteral -Schema $Schema
        }
    }
    process {
        # Create a new OpenAPI header for the specified document(s) and return it
        foreach ($doc in $DocId) {
            $docDescriptor = $Server.GetOrCreateOpenApiDocument($doc)
            $response = $docDescriptor.NewOpenApiResponse(
                $Summary,
                $Description,
                $Headers,
                $Links,
                $Content
            )
            return $response
        }
    }
}
