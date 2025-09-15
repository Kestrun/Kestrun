<#
.SYNOPSIS
    Logs a warning that a function must be called inside a route script block.
.DESCRIPTION
    This function logs a warning message indicating that a specific function must be called within a route script
    block where the $Context variable is available.
.PARAMETER FunctionName
    The name of the function that is being called outside of a route script block.
    Defaults to the name of the calling function.
.EXAMPLE
    Write-KrOutsideRouteWarning -FunctionName 'Invoke-KrCookieSignIn'
    This example logs a warning that 'Invoke-KrCookieSignIn' must be called inside a route script block.
.NOTES
    This function is intended for internal use within the Kestrun framework.
#>
function Write-KrOutsideRouteWarning {
    param (
        [string]$FunctionName = $PSCmdlet.MyInvocation.InvocationName
    )
    if (Test-KrLogger) {
        Write-KrLog -Level Warning -Message '{function} must be called inside a route script block where $Context is available.' -Properties $FunctionName
    } else {
        Write-Warning -Message "$FunctionName must be called inside a route script block where `$Context is available."
    }
}
