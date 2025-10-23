<#
    .SYNOPSIS
        Retrieves a request header value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the specified request header by name.
    .PARAMETER Name
        The name of the request header to retrieve from the HTTP request.
    .PARAMETER AsInt
        If specified, converts the header value to an integer.
    .PARAMETER AsBool
        If specified, converts the header value to a boolean.
    .PARAMETER AsDouble
        If specified, converts the header value to a double.
    .PARAMETER AsString
        If specified, converts the header value to a string.
    .PARAMETER ThrowIfMissing
        If specified, throws an error if the header is not found.
        By default, it returns $null if not found.
    .EXAMPLE
        $value = Get-KrRequestHeader -Name "param1"
        Retrieves the value of the request header "param1" from the HTTP request.
    .EXAMPLE
        $id = Get-KrRequestHeader -Name "id" -AsInt -ThrowIfMissing
        Retrieves the value of the header "id" as an integer, throwing an error if it's missing.
    .EXAMPLE
        $flag = Get-KrRequestHeader -Name "flag" -AsBool
        Retrieves the value of the header "flag" as a boolean.
    .EXAMPLE
        $auth = Get-KrRequestHeader -Name "Authorization" -AsString -ThrowIfMissing
        Retrieves the value of the "Authorization" header as a string, throwing an error if it's missing.
    .OUTPUTS
        Returns the value of the specified request header, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestHeader {
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
        [Alias('AsBoolean')]
        [switch]$AsBool,

        [Parameter(parameterSetName = 'Double')]
        [switch]$AsDouble,

        [Parameter(parameterSetName = 'String')]
        [switch]$AsString,

        [Parameter()]
        [switch]$ThrowIfMissing
    )
    if ($null -ne $Context -and $null -ne $Context.Request -and $null -ne $Context.Request.Headers) {
        if ($ThrowIfMissing -and -not $Context.Request.Headers.ContainsKey($Name)) {
            throw [System.ArgumentException]::new("Missing required header: $Name")
        }
        # Get the request header value from the request
        $value = $Context.Request.Headers[$Name]
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

