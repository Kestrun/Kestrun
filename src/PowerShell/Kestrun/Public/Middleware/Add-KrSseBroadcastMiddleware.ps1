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
    .PARAMETER KeepAliveSeconds
        If greater than 0, sends periodic SSE comments (keep-alives) to keep intermediaries from closing idle connections.
    .PARAMETER PassThru
        If specified, returns the modified server instance.
    .EXAMPLE
        Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -PassThru
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

        [Parameter(Mandatory = $false)]
        [ValidateRange(0, 3600)]
        [int]$KeepAliveSeconds = 15,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        [Kestrun.Hosting.KestrunHostSseExtensions]::AddSseBroadcast($Server, $Path, $KeepAliveSeconds) | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
