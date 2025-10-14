<#
    .SYNOPSIS
        Retrieves a query parameter value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified query parameter by name.
    .PARAMETER Name
        The name of the query parameter to retrieve from the HTTP request.
    .PARAMETER AsInt
        If specified, converts the query parameter value to an integer.
    .PARAMETER AsBool
        If specified, converts the query parameter value to a boolean.
    .PARAMETER AsDouble
        If specified, converts the query parameter value to a double.
    .PARAMETER AsString
        If specified, converts the query parameter value to a string.
    .PARAMETER ThrowIfMissing
        If specified, throws an error if the query parameter is not found.
        By default, it returns $null if not found.
    .EXAMPLE
        $value = Get-KrRequestQuery -Name "param1"
        Retrieves the value of the query parameter "param1" from the HTTP request.
    .EXAMPLE
        $id = Get-KrRequestQuery -Name "id" -AsInt -ThrowIfMissing
        Retrieves the value of the query parameter "id" as an integer, throwing an error if it's missing.
    .EXAMPLE
        $flag = Get-KrRequestQuery -Name "flag" -AsBool
        Retrieves the value of the query parameter "flag" as a boolean.
    .OUTPUTS
        Returns the value of the specified query parameter, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestQuery {
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
    if ($null -ne $Context -and $null -ne $Context.Request -and $null -ne $Context.Request.Query) {
        if ($ThrowIfMissing -and -not $Context.Request.Query.ContainsKey($Name)) {
            throw [System.ArgumentException]::new("Missing required query parameter: $Name")
        }
        # Get the query parameter value from the request
        $value = $Context.Request.Query[$Name]
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
        # Outside of request context
        Write-KrOutsideRouteWarning
    }
}

