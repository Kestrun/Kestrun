<#
.SYNOPSIS
    Logs an error message and optionally stops the Kestrun server.
.DESCRIPTION
    This function logs an error message using the Kestrun logging framework. If the Terminate switch is specified, it will also stop the Kestrun server.
.PARAMETER FunctionName
    The name of the function or cmdlet where the error occurred. Defaults to the current invocation name.
.PARAMETER ErrorMessage
    The error message to log.
.PARAMETER Terminate
    If specified, the function will stop the Kestrun server after logging the error.
.EXAMPLE
    Write-KrError -FunctionName 'Start-KrServer' -ErrorMessage 'Failed to start the server.' -Terminate
    This example logs an error message indicating that the server failed to start and then stops the Kestrun server.
.NOTES
    This function is intended for internal use within the Kestrun framework.
#>
function Write-KrError {
    param (
        [string]$FunctionName = $PSCmdlet.MyInvocation.InvocationName,
        [string]$ErrorMessage,
        [switch]$Terminate
    )
    if (Test-KrLogger) {
        Write-KrLog -Level Error -Message '{function}: {message}' -Values $FunctionName, $ErrorMessage
    } else {
        Write-Warning -Message "$($FunctionName): $ErrorMessage"
    }
    if ($Terminate) {
        Stop-KrServer -NoWait
    }
}
