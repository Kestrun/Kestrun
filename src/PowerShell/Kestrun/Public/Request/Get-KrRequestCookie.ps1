<#
    .SYNOPSIS
        Retrieves a cookie value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified cookie by name.
    .PARAMETER Name
        The name of the cookie to retrieve from the HTTP request.
    .PARAMETER AsInt
        If specified, converts the cookie value to an integer.
    .PARAMETER AsBool
        If specified, converts the cookie value to a boolean.
    .PARAMETER AsDouble
        If specified, converts the cookie value to a double.
    .PARAMETER AsString
        If specified, converts the cookie value to a string.
    .PARAMETER ThrowIfMissing
        If specified, throws an error if the cookie is not found.
        By default, it returns $null if not found.
    .EXAMPLE
        $value = Get-KrRequestCookie -Name "param1"
        Retrieves the value of the cookie "param1" from the HTTP request.
    .EXAMPLE
        $id = Get-KrRequestCookie -Name "id" -AsInt -ThrowIfMissing
        Retrieves the value of the cookie "id" as an integer, throwing an error if it's missing.
    .EXAMPLE
        $flag = Get-KrRequestCookie -Name "flag" -AsBool
        Retrieves the value of the cookie "flag" as a boolean.
    .OUTPUTS
        Returns the value of the specified cookie, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestCookie {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    [OutputType([string])]
    [OutputType([int])]
    [OutputType([bool])]
    [OutputType([double])]
    [OutputType([object])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(parameterSetName = 'Int')]
        [switch]$AsInt,

        [Parameter(parameterSetName = 'Bool')]
        [Alias('AsBoolean')]
        [switch]$AsBool,

        [Parameter(parameterSetName = 'Double')]
        [switch]$AsDouble,

        [Parameter(parameterSetName = 'String')]
        [switch]$AsString,

        [Parameter()]
        [switch]$ThrowIfMissing
    )
    if ($null -ne $Context -and $null -ne $Context.Request -and $null -ne $Context.Request.Cookies) {
        if ($ThrowIfMissing -and -not $Context.Request.Cookies.ContainsKey($Name)) {
            throw [System.ArgumentException]::new("Missing required cookie: $Name", "$Name")
        }
        # Get the cookie value from the request
        $value = $Context.Request.Cookies[$Name]

        if ($AsInt) {
            return [int]$value
        }
        if ($AsBool) {
            return [bool]$value
        }
        if ($AsDouble) {
            return [double]$value
        }
        if ($AsString) {
            return $value.ToString()
        }
        return $value
    } else {
        # Outside of route context
        Write-KrOutsideRouteWarning
    }
}
