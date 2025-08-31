<#
    .SYNOPSIS
        Retrieves a request body value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the request body.
    .PARAMETER Raw
        If specified, retrieves the raw request body without any parsing.
    .EXAMPLE
        $value = Get-KrRequestBody
        Retrieves the value of the request body from the HTTP request.
    .EXAMPLE
        $value = Get-KrRequestBody -Raw
        Retrieves the raw request body from the HTTP request without any parsing.
    .OUTPUTS
        Returns the value of the request body, or $null if not found.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Get-KrRequestBody {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    [OutputType([Hashtable])]
    param(
        [switch]$Raw
    )
    if ($null -ne $Context.Request) {
        if ($Raw) {
            # Get the raw request body value from the request
            return $Context.Request.Body
        }
        switch ($Context.Request.ContentType) {
            'application/json' {
                return $Context.Request.Body | ConvertFrom-Json -AsHashtable
            }
            'application/yaml' {
                return [Kestrun.Utilities.YamlHelper]::ToHashtable( $Context.Request.Body)
            }
            'application/x-www-form-urlencoded' {
                return $Context.Request.Form
            }
            'application/xml' {
                return [Kestrun.Utilities.XmlHelper]::ToHashtable( $Context.Request.Body)
            }
            default {
                return $Context.Request.Body
            }
        }
    }
}
