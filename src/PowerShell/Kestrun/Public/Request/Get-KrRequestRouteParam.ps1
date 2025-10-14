<#
    .SYNOPSIS
        Retrieves a request route value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified request route value by name.
    .PARAMETER Name
        The name of the request route value to retrieve from the HTTP request.
    .PARAMETER AsInt
        If specified, converts the route value to an integer.
    .PARAMETER AsBool
        If specified, converts the route value to a boolean.
    .PARAMETER AsDouble
        If specified, converts the route value to a double.
    .PARAMETER AsString
        If specified, converts the route value to a string.
    .PARAMETER ThrowIfMissing
        If specified, throws an error if the route value is not found.
        By default, it returns $null if not found.
    .EXAMPLE
        $value = Get-KrRequestRouteParam -Name "param1"
        Retrieves the value of the request route value "param1" from the HTTP request.
    .EXAMPLE
        $id = Get-KrRequestRouteParam -Name "id" -AsInt -ThrowIfMissing
        Retrieves the value of the route parameter "id" as an integer, throwing an error if it's missing.
    .EXAMPLE
        $flag = Get-KrRequestRouteParam -Name "flag" -AsBool
        Retrieves the value of the route parameter "flag" as a boolean.
    .OUTPUTS
        Returns the value of the specified request route value, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestRouteParam {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(defaultParameterSetName = 'default')]
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
        [switch]$AsBool,

        [Parameter(parameterSetName = 'Double')]
        [switch]$AsDouble,

        [Parameter(parameterSetName = 'String')]
        [switch]$AsString,

        [Parameter()]
        [switch]$ThrowIfMissing
    )
    if ($null -ne $Context) {
        if ($ThrowIfMissing -and -not $Context.Request.RouteValues.ContainsKey($Name)) {
            throw "Missing required route parameter: $Name"
        }
        # Get the route parameter value from the request
        $value = $Context.Request.RouteValues[$Name]

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
