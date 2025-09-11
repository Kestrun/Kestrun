<#
    .SYNOPSIS
        Writes an error response to the HTTP client.
    .DESCRIPTION
        This function allows you to send an error message or exception details back to the client.
    .PARAMETER Message
        The error message to send in the response. This is used when the error is a simple
        message rather than an exception.
    .PARAMETER Exception
        The exception object containing error details. This is used when you want to send
        detailed exception information back to the client.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 500 (Internal Server Error).
    .PARAMETER ContentType
        The content type of the response. If not specified, defaults to "application/json".
    .PARAMETER Details
        Additional details to include in the error response. This can be used to provide
        more context about the error.
    .PARAMETER IncludeStack
        A switch to indicate whether to include the stack trace in the error response. This is useful for debugging purposes.
    .EXAMPLE
        Write-KrErrorResponse -Message "An error occurred while processing your request." -StatusCode 400 -ContentType "application/json"
        Writes a simple error message to the response with a 400 Bad Request status code and content type "application/json".
    .EXAMPLE
        Write-KrErrorResponse -Exception $exception -StatusCode 500 -ContentType "application/json" -IncludeStack
        Writes the details of the provided exception to the response with a 500 Internal Server Error status
        code and content type "application/json". The stack trace is included in the response.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrErrorResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(DefaultParameterSetName = 'Message')]
    param (
        [Parameter(ParameterSetName = 'Message', Mandatory = $true)]
        [string]$Message,

        [Parameter(ParameterSetName = 'Exception', Mandatory = $true)]
        [System.Exception]$Exception,

        [Parameter()]
        [int]$StatusCode = 500,

        [Parameter()]
        [string]$ContentType,

        [Parameter()]
        [string]$Details,

        [Parameter()]
        [switch]$IncludeStack
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        if ($PSCmdlet.ParameterSetName -eq 'Message') {
            $Context.Response.WriteErrorResponse(
                $Message,
                $StatusCode,
                $ContentType,
                $Details
            )
        } else {
            $Context.Response.WriteErrorResponse(
                $Exception,
                $StatusCode,
                $ContentType,
                $IncludeStack.IsPresent
            )
        }
    } else {
        Write-KrOutsideRouteWarning
    }
}

