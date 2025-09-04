<#
    .SYNOPSIS
        Writes an object to the HTTP response body as JSON.
    .DESCRIPTION
        Serializes the provided object to JSON using Newtonsoft.Json and writes it
        to the current HTTP response. The caller can specify the HTTP status code,
        serialization depth and formatting options.
    .PARAMETER InputObject
        The object to serialize and write to the response.
    .PARAMETER StatusCode
        The HTTP status code to set for the response.
    .PARAMETER Depth
        The maximum depth of the JSON serialization.
    .PARAMETER Compress
        Whether to compress the JSON output.
    .PARAMETER ContentType
        The content type of the response.
    .EXAMPLE
        PS> $myObject | Write-KrJsonResponse -StatusCode 201 -Depth 5 -Compress -ContentType "application/json"
        Serializes the object to JSON and writes it to the response with the specified options.
    .EXAMPLE
        PS> $myObject | Write-KrJsonResponse -StatusCode 400 -Depth 3 -Compress -ContentType "application/json"
        Serializes the object to JSON and writes it to the response with the specified options.
    .EXAMPLE
        PS> $myObject | Write-KrJsonResponse -StatusCode 500 -Depth 2
        Serializes the object to JSON and writes it to the response with the specified options.
#>
function Write-KrJsonResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object]$InputObject,

        [Parameter()]
        [int]$StatusCode = 200,

        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$Depth = 10,

        [Parameter()]
        [bool]$Compress = $false,

        [Parameter()]
        [string]$ContentType
    )
    process {
        if ($null -ne $Context.Response) {
            $ContentType = [string]::IsNullOrEmpty($ContentType) ? 'application/json' : $ContentType
            $Context.Response.WriteTextResponse((ConvertTo-Json -InputObject $InputObject -Depth $Depth -Compress:$Compress), $StatusCode, $ContentType)

            <# To use the C# method directly, uncomment the following lines:
        # Create a new JsonSerializerSettings object with the specified options
        $serializerSettings = [Newtonsoft.Json.JsonSerializerSettings]::new()
        $serializerSettings.Formatting = if ($Compress) { [Newtonsoft.Json.Formatting]::None } else { [Newtonsoft.Json.Formatting]::Indented }
        $serializerSettings.ContractResolver = [Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver]::new()
        $serializerSettings.ReferenceLoopHandling = [Newtonsoft.Json.ReferenceLoopHandling]::Ignore
        $serializerSettings.NullValueHandling = [Newtonsoft.Json.NullValueHandling]::Ignore
        $serializerSettings.DefaultValueHandling = [Newtonsoft.Json.DefaultValueHandling]::Ignore
        $serializerSettings.MaxDepth = $Depth
        $serializerSettings.DateFormatHandling = [Newtonsoft.Json.DateFormatHandling]::IsoDateFormat
        # Call the C# method on the $Context.Response object
        $Context.Response.WriteJsonResponse($InputObject, $serializerSettings, $StatusCode, $ContentType)#>
        }
    }
}

