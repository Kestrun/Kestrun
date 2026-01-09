<#
    .SYNOPSIS
        Starts a Server-Sent Events (SSE) response to the client.
    .DESCRIPTION
        This function initiates a Server-Sent Events (SSE) response to the connected client.
        It sets the appropriate headers and keeps the connection open for sending SSEs.
    .EXAMPLE
        Start-KrSseResponse
        Starts an SSE response to the client.
    .NOTES
        This function requires that SSE has been configured on the server using Add-KrSseMiddleware.
#>
function Start-KrSseResponse {
    [KestrunRuntimeApi('Route')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param()

    # Only works inside a route script block where $Context is available
    if ($null -ne $Context) {
        $Context.StartSse()
        return
    }
    # Only works inside a route script block where $Context is available
    Write-KrOutsideRouteWarning
}
