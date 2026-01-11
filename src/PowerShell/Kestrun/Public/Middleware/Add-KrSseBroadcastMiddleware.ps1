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
        The OpenAPI document IDs to which the SSE broadcast endpoint should be added. Default is 'Default'.
    .PARAMETER KeepAliveSeconds
        If greater than 0, sends periodic SSE comments (keep-alives) to keep intermediaries from closing idle connections.
    .PARAMETER OperationId
        Optional OpenAPI operationId override for the broadcast endpoint.
    .PARAMETER Summary
        Optional OpenAPI summary override for the broadcast endpoint.
    .PARAMETER Description
        Optional OpenAPI description override for the broadcast endpoint.
    .PARAMETER Tags
        Optional OpenAPI tags override for the broadcast endpoint.
    .PARAMETER StatusCode
        Optional OpenAPI response status code override (default: 200).
    .PARAMETER ResponseDescription
        Optional OpenAPI response description override.
    .PARAMETER ItemSchemaType
        Optional OpenAPI schema type for the stream payload (default: String).
        This only applies when -OpenApi is not provided.
    .PARAMETER SkipOpenApi
        If specified, the OpenAPI documentation for this endpoint will be skipped.
    .PARAMETER Options
        Full OpenAPI customization object (Kestrun.Hosting.Options.SseBroadcastOptions).
        When provided, it takes precedence over the individual parameters.
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
        Add-KrSseBroadcastMiddleware -SkipOpenApi
        Adds an SSE broadcast endpoint without OpenAPI documentation.
    .EXAMPLE
        $options = [Kestrun.Hosting.Options.SseBroadcastOptions]::new()
        $options.Path = '/sse/updates'
        $options.KeepAliveSeconds = 15
        Add-KrSseBroadcastMiddleware -Options $options -PassThru
        Adds an SSE broadcast endpoint at '/sse/updates' with 15-second keep-alives using the provided options object.
    .NOTES
        Call this before Enable-KrConfiguration.
#>
function Add-KrSseBroadcastMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Items')]
    param(
        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ItemsSkipOpenApi')]
        [string]$Path,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ItemsSkipOpenApi')]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [ValidateRange(0, 3600)]
        [int]$KeepAliveSeconds,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$OperationId,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$Summary,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$Description,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string[]]$Tags,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [ValidatePattern('^(default|\\d{3})$')]
        [string]$StatusCode,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$ResponseDescription,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [object]$ItemSchemaType = [string],

        [Parameter(Mandatory = $false, ParameterSetName = 'ItemsSkipOpenApi')]
        [switch]$SkipOpenApi,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Hosting.Options.SseBroadcastOptions]$Options,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Kestrun.Hosting.Options.SseBroadcastOptions]::new()
            if ( $SkipOpenApi.IsPresent ) {
                $Options.SkipOpenApi = $true
            }# Set the documentation IDs for the SSE broadcast endpoint
            $Options.DocId = $DocId

            # Set the path for the SSE broadcast endpoint
            if ($PSBoundParameters.ContainsKey('Path')) { $Options.Path = $Path }
            if ($PSBoundParameters.ContainsKey('KeepAliveSeconds')) { $Options.KeepAliveSeconds = $KeepAliveSeconds }
            if ($PSBoundParameters.ContainsKey('OperationId')) { $Options.OperationId = $OperationId }
            if ($PSBoundParameters.ContainsKey('Summary')) { $Options.Summary = $Summary }
            if ($PSBoundParameters.ContainsKey('Description')) { $Options.Description = $Description }
            if ($PSBoundParameters.ContainsKey('Tags')) { $Options.Tags = $Tags }
            if ($PSBoundParameters.ContainsKey('StatusCode')) { $Options.StatusCode = $StatusCode }
            if ($PSBoundParameters.ContainsKey('ResponseDescription')) { $Options.ResponseDescription = $ResponseDescription }
            if ($PSBoundParameters.ContainsKey('ItemSchemaType')) {
                $Options.ItemSchemaType = $ItemSchemaType
            }
        }

        $Server.AddSseBroadcast($Options) | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
