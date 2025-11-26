<#
    .SYNOPSIS
        Retrieves a request body value from the HTTP request.
    .DESCRIPTION
        This function accesses the current HTTP request context and retrieves the value
        of the request body.
    .PARAMETER Raw
        If specified, retrieves the raw request body without any parsing.
    .PARAMETER Type
        Specifies the type to which the request body should be deserialized.
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
        [switch]$Raw,
        [Type]$Type
    )

    if ($null -ne $Context.Request) {
        $body = $Context.Request.Body
        # Return the raw body if specified
        if ($Raw) {
            # Get the raw request body value from the request
            return $body
        }
        # Parse the request body based on the specified type or content type
        if ($null -ne $Type) {
            return [Kestrun.Utilities.Json.JsonSerializerHelper]::FromJson($body, $Type)
        }
        # Parse based on Content-Type
        switch ($Context.Request.ContentType) {
            'application/json' {
                return $body | ConvertFrom-Json -AsHashtable
            }
            'application/yaml' {
                return [Kestrun.Utilities.YamlHelper]::ToHashtable( $body)
            }
            'application/x-www-form-urlencoded' {
                return $Context.Request.Form
            }
            'application/xml' {
                return [Kestrun.Utilities.XmlHelper]::ToHashtable( $body)
            }
            default {
                return $body
            }
        }
    }
}
