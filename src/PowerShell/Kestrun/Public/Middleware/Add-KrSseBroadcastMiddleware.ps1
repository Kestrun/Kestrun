<#
    .SYNOPSIS
        Adds an SSE broadcast endpoint to the server.
    .DESCRIPTION
        Registers an in-memory SSE broadcaster service and maps an SSE endpoint that keeps connections open.
        Clients connect (e.g. via browser EventSource) and receive events broadcast by Send-KrSseBroadcastEvent.
    .PARAMETER Server
        The Kestrun server instance. If not provided, the default server is used.
    .PARAMETER Path
        The URL path where the SSE broadcast endpoint will be accessible. Defaults to '/sse/broadcast'.
    .PARAMETER DocId
        The OpenAPI document IDs to which the SSE broadcast endpoint should be added. Default is '
    .PARAMETER KeepAliveSeconds
        If greater than 0, sends periodic SSE comments (keep-alives) to keep intermediaries from closing idle connections.
    .PARAMETER OpenApiOperationId
        Optional OpenAPI operationId override for the broadcast endpoint.
    .PARAMETER OpenApiSummary
        Optional OpenAPI summary override for the broadcast endpoint.
    .PARAMETER OpenApiDescription
        Optional OpenAPI description override for the broadcast endpoint.
    .PARAMETER OpenApiTags
        Optional OpenAPI tags override for the broadcast endpoint.
    .PARAMETER OpenApiStatusCode
        Optional OpenAPI response status code override (default: 200).
    .PARAMETER OpenApiResponseDescription
        Optional OpenAPI response description override.
    .PARAMETER ItemSchemaType
        Optional OpenAPI schema type for the stream payload (default: String).
        This only applies when -OpenApi is not provided.
    .PARAMETER OpenApi
        Full OpenAPI customization object (Kestrun.Hosting.Options.SseBroadcastOpenApiOptions).
        When provided, it takes precedence over the individual -OpenApi* parameters.
    .PARAMETER PassThru
        If specified, returns the modified server instance.
    .EXAMPLE
        Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -PassThru
        Adds an SSE broadcast endpoint at '/sse/broadcast' and returns the server instance.
    .EXAMPLE
        $server = New-KrServer -Name 'MyServer'
        Add-KrSseBroadcastMiddleware -Server $server -Path '/events' -KeepAliveSeconds 30
        Adds an SSE broadcast endpoint at '/events' with 30-second keep-alives to the specified server.
    .EXAMPLE
        Add-KrSseBroadcastMiddleware -OpenApi (New-Object Kestrun.Hosting.Options.SseBroadcastOpenApiOptions -Property @{
            OperationId = 'SseBroadcast'
            Summary = 'SSE Broadcast Endpoint'
            Description = 'Streams server-sent events to connected clients.'
            Tags = @('SSE', 'Broadcast')
            StatusCode = '200'
            ResponseDescription = 'Stream of server-sent events'
            ItemSchemaType = [MyCustomEventType]
        })
        Adds an SSE broadcast endpoint with custom OpenAPI documentation.
    .NOTES
        Call this before Enable-KrConfiguration.
#>
function Add-KrSseBroadcastMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $false)]
        [string]$Path = '/sse/broadcast',

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter(Mandatory = $false)]
        [ValidateRange(0, 3600)]
        [int]$KeepAliveSeconds = 15,

        [Parameter(Mandatory = $false)]
        [string]$OpenApiOperationId,

        [Parameter(Mandatory = $false)]
        [string]$OpenApiSummary,

        [Parameter(Mandatory = $false)]
        [string]$OpenApiDescription,

        [Parameter(Mandatory = $false)]
        [string[]]$OpenApiTags,

        [Parameter(Mandatory = $false)]
        [ValidatePattern('^(default|\\d{3})$')]
        [string]$OpenApiStatusCode,

        [Parameter(Mandatory = $false)]
        [string]$OpenApiResponseDescription,

        [Parameter(Mandatory = $false)]
        [object]$ItemSchemaType = [string],

        [Parameter(Mandatory = $false)]
        [Kestrun.Hosting.Options.SseBroadcastOpenApiOptions]$OpenApi,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $openApiOptions = $OpenApi

        if ($null -eq $openApiOptions) {
            $hasOpenApiOverrides = $PSBoundParameters.ContainsKey('OpenApiOperationId') -or
            $PSBoundParameters.ContainsKey('OpenApiSummary') -or
            $PSBoundParameters.ContainsKey('OpenApiDescription') -or
            $PSBoundParameters.ContainsKey('OpenApiTags') -or
            $PSBoundParameters.ContainsKey('OpenApiStatusCode') -or
            $PSBoundParameters.ContainsKey('OpenApiResponseDescription') -or
            $PSBoundParameters.ContainsKey('ItemSchemaType')

            if ($hasOpenApiOverrides) {
                $openApiProps = @{}

                if ($PSBoundParameters.ContainsKey('OpenApiOperationId')) { $openApiProps.OperationId = $OpenApiOperationId }
                if ($PSBoundParameters.ContainsKey('OpenApiSummary')) { $openApiProps.Summary = $OpenApiSummary }
                if ($PSBoundParameters.ContainsKey('OpenApiDescription')) { $openApiProps.Description = $OpenApiDescription }
                if ($PSBoundParameters.ContainsKey('OpenApiTags')) { $openApiProps.Tags = $OpenApiTags }
                if ($PSBoundParameters.ContainsKey('OpenApiStatusCode')) { $openApiProps.StatusCode = $OpenApiStatusCode }
                if ($PSBoundParameters.ContainsKey('OpenApiResponseDescription')) { $openApiProps.ResponseDescription = $OpenApiResponseDescription }

                if ($PSBoundParameters.ContainsKey('ItemSchemaType')) {
                    $openApiProps.ItemSchemaType = $ItemSchemaType
                }

                $openApiOptions = New-Object 'Kestrun.Hosting.Options.SseBroadcastOpenApiOptions' -Property $openApiProps
            }
        }

        [Kestrun.Hosting.KestrunHostSseExtensions]::AddSseBroadcast($Server, $Path, $KeepAliveSeconds, $openApiOptions, $DocId) | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
