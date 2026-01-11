<#
    Create a broadcast SSE demo server with Kestrun in PowerShell (with OpenAPI).
    FileName: 10.21-OpenAPI-SseBroadcast.ps1

    This demo shows:
    - GET /sse/broadcast : server-side broadcast stream (text/event-stream)
    - POST /api/broadcast: broadcast an event to all connected SSE clients

    Broadcast SSE pieces:
    - Add-KrSseBroadcastMiddleware (registers broadcaster + maps /sse/broadcast)
    - Send-KrSseBroadcastEvent (broadcasts to all connected clients)

    Per-request SSE helpers (still available):
    - Start-KrSseResponse
    - Write-KrSseEvent
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

Initialize-KrRoot -Path $PSScriptRoot

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'SseBroadcastDemoOpenApi' -SetAsDefault

New-KrServer -Name 'Kestrun SSE Broadcast Demo (OpenAPI)'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Kestrun SSE Broadcast Demo API' `
    -Version '1.0.0' `
    -Description 'Demonstrates documenting an SSE broadcast endpoint (text/event-stream) and a broadcast trigger API.'

# =========================================================
#           OPENAPI SCHEMA COMPONENT DEFINITIONS
# =========================================================

[OpenApiSchemaComponent(RequiredProperties = ('event', 'data'))]
class SseBroadcastRequest {
    [OpenApiProperty(Description = 'SSE event name', Example = 'message')]
    [string]$event

    [OpenApiProperty(Description = 'Arbitrary payload object')]
    [object]$data
}

[OpenApiSchemaComponent(RequiredProperties = ('ok', 'event', 'connected'))]
class SseBroadcastOkResponse {
    [OpenApiProperty(Description = 'True when broadcast succeeded', Example = $true)]
    [bool]$ok

    [OpenApiProperty(Description = 'Event name that was broadcast', Example = 'message')]
    [string]$event

    [OpenApiProperty(Description = 'Connected SSE client count (null if unavailable)', Example = 3)]
    [Nullable[int]]$connected
}

[OpenApiSchemaComponent(RequiredProperties = ('ok', 'error'))]
class SseBroadcastErrorResponse {
    [OpenApiProperty(Description = 'False when broadcast failed', Example = $false)]
    [bool]$ok

    [OpenApiProperty(Description = 'Error message')]
    [string]$error
}


[OpenApiSchemaComponent(Description = 'Operation progress SSE event payload')]
class OperationProgressEvent {
    [OpenApiProperty(Description = 'Task identifier')]
    [string]$taskId

    [OpenApiProperty(Description = 'Progress percent', Format = 'double')]
    [double]$progress

    [OpenApiProperty(Description = 'Human status message')]
    [string]$status

    [OpenApiProperty(Description = 'State text')]
    [string]$state

    [OpenApiProperty(Description = 'UTC timestamp', Format = 'date-time')]
    [datetime]$ts
}

# Add the broadcast SSE endpoint (implemented in C#; keeps connections open)
# Note: the C# endpoint registers its own OpenAPI metadata so it will appear in the OpenAPI document.
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -KeepAliveSeconds 15

# 2) Progress broadcast SSE stream (OpenAPI payload schema: OperationProgressEvent)
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast/progress' -KeepAliveSeconds 15 -ItemSchemaType ([OperationProgressEvent])
# =========================================================

## Enable Configuration
Enable-KrConfiguration

# Swagger / Redoc UI routes
Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'

# =========================================================
#                       HOME PAGE
# =========================================================

Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath 'Assets/wwwroot/sseBroadcast.html'


# =========================================================
#                OPENAPI-ANNOTATED ROUTES
# =========================================================

<#
.SYNOPSIS
    Broadcasts an SSE event to all connected clients.
.DESCRIPTION
    Accepts a JSON payload containing an SSE event name and an arbitrary data object.
    The server broadcasts the event to all connected clients of GET /sse/broadcast.
.PARAMETER body
    Broadcast request payload.
#>
function InvokeSseBroadcast {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/broadcast', Tags = 'SSE')]
    [OpenApiResponse(StatusCode = '200', Description = 'Broadcast succeeded', Schema = [SseBroadcastOkResponse], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '500', Description = 'Broadcast failed', Schema = [SseBroadcastErrorResponse], ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(Description = 'Broadcast SSE event payload', Required = $true)]
        [SseBroadcastRequest]$body
    )

    try {
        $eventName = [string]($body.event ?? 'message')

        $dataObj = $body.data
        if ($null -eq $dataObj) { $dataObj = @{ text = 'empty' } }

        $dataJson = $dataObj | ConvertTo-Json -Compress

        Send-KrSseBroadcastEvent -Event $eventName -Data $dataJson

        $count = Get-KrSseConnectedClientCount

        $res = [SseBroadcastOkResponse]::new()
        $res.ok = $true
        $res.event = $eventName
        $res.connected = $count

        Write-KrJsonResponse -InputObject $res -StatusCode 200
    } catch {
        Write-KrLog -Level Error -Exception $_.Exception -Message 'InvokeSseBroadcast failed: {error}' -Values $_.ToString()
        # Generate error response
        $err = [SseBroadcastErrorResponse]::new()
        $err.ok = $false
        $err.error = 'Broadcast failed. See server logs for details.'
        Write-KrJsonResponse -InputObject $err -StatusCode 500
    }
}

<#
.SYNOPSIS
    Broadcasts a progress SSE event to all connected clients.
.DESCRIPTION
    Accepts an OperationProgressEvent payload and broadcasts it as a 'progress' SSE event.
    Clients can connect to either GET /sse/broadcast or GET /sse/broadcast/progress.
.PARAMETER body
    Progress event payload.
#>
function InvokeSseBroadcastProgress {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/broadcast/progress', Tags = 'SSE')]
    [OpenApiResponse(StatusCode = '200', Description = 'Broadcast succeeded', Schema = [SseBroadcastOkResponse], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '500', Description = 'Broadcast failed', Schema = [SseBroadcastErrorResponse], ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(Description = 'Progress event payload', Required = $true)]
        [OperationProgressEvent]$body
    )

    try {
        if ($null -eq $body.ts -or $body.ts -lt [datetime]'2000-01-01') {
            $body.ts = [datetime]::UtcNow
        }

        $dataJson = $body | ConvertTo-Json -Compress

        Send-KrSseBroadcastEvent -Event 'progress' -Data $dataJson

        $count = Get-KrSseConnectedClientCount

        $res = [SseBroadcastOkResponse]::new()
        $res.ok = $true
        $res.event = 'progress'
        $res.connected = $count

        Write-KrJsonResponse -InputObject $res -StatusCode 200
    } catch {
        Write-KrLog -Level Error -Exception $_.Exception -Message 'InvokeSseBroadcast failed: {error}' -Values $_.ToString()
        # Generate error response
        $err = [SseBroadcastErrorResponse]::new()
        $err.ok = $false
        $err.error = 'Broadcast failed. See server logs for details.'
        Write-KrJsonResponse -InputObject $err -StatusCode 500
    }
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

Build-KrOpenApiDocument
Test-KrOpenApiDocument

Write-Host '🟢 Kestrun SSE Broadcast Demo (OpenAPI) Server Started' -ForegroundColor Green
Write-Host "📍 Navigate to http://localhost:$Port" -ForegroundColor Cyan
Write-Host "📡 Broadcast SSE endpoint: http://localhost:$Port/sse/broadcast" -ForegroundColor Cyan
Write-Host "📈 Progress SSE endpoint: http://localhost:$Port/sse/broadcast/progress" -ForegroundColor Cyan
Write-Host "📨 Broadcast API: http://localhost:$Port/api/broadcast" -ForegroundColor Cyan
Write-Host "📨 Progress Broadcast API: http://localhost:$Port/api/broadcast/progress" -ForegroundColor Cyan
Write-Host "📄 OpenAPI JSON: http://localhost:$Port/openapi/v3.1/openapi.json" -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
