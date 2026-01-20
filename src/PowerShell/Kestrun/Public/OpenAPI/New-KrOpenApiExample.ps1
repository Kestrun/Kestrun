<#
.SYNOPSIS
    Creates a new OpenAPI Component Example object.
.DESCRIPTION
    This cmdlet creates a new OpenAPI Component Example object that can be used in OpenAPI specifications.
.PARAMETER Server
    The Kestrun server instance to use. If not specified, the default server instance is used.
.PARAMETER Summary
    A short summary of what the example is about.
.PARAMETER Description
    A verbose explanation of the example.
.PARAMETER ExternalValue
    A URL that points to the literal example.
.PARAMETER Value
    The actual example payload. Can be hashtable/pscustomobject/string/number/etc.
.PARAMETER DataValue
    The actual example payload as an OpenAPI 3.2 native field. In OpenAPI 3.1, it serializes as x-oai-dataValue.
.PARAMETER SerializedValue
    The serialized representation of the DataValue. In OpenAPI 3.1, it serializes as x-oai-serializedValue.
.PARAMETER Extensions
    A dictionary of OpenAPI extensions to add to the example.
.OUTPUTS
    Microsoft.OpenApi.OpenApiExample object.
.EXAMPLE
    $example = New-KrOpenApiExample -Summary "User Example" -Description "An example of a user object." -Value @{ id = 1; name = "John Doe" }
    This example creates a new OpenAPI Component Example with a summary, description, and a value.
.EXAMPLE
    $example = New-KrOpenApiExample -Summary "External Example" -ExternalValue "http://example.com/example.json"
    This example creates a new OpenAPI Component Example that references an external example.
.EXAMPLE
    $dataValue = @{ id = 2; name = "Jane Doe" }
    $example = New-KrOpenApiExample -Summary "Data Value Example" -DataValue $dataValue -SerializedValue '{"id":2,"name":"Jane Doe"}'
    This example creates a new OpenAPI Component Example using the DataValue and SerializedValue parameters.
.EXAMPLE
    $dataValue = @{ id = 3; name = "Alice" }
    $example = New-KrOpenApiExample -Summary "Auto Serialized Value Example" -DataValue $dataValue
    This example creates a new OpenAPI Component Example using the DataValue parameter and automatically serializes it to JSON for the SerializedValue.
.NOTES
#>
function New-KrOpenApiExample {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Value')]
    [OutputType([Microsoft.OpenApi.OpenApiExample])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Summary,

        [Parameter()]
        [string]$Description,

        [Parameter(Mandatory = $true, ParameterSetName = 'ExternalValue')]
        [string]$ExternalValue,

        [Parameter(Mandatory = $true, ParameterSetName = 'Value')]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true, ParameterSetName = 'DataValue')]
        [AllowNull()]
        [object] $DataValue,

        [Parameter(ParameterSetName = 'DataValue')]
        [string] $SerializedValue,

        [Parameter()]
        [System.Collections.IDictionary]$Extensions
    )

    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Create example for the specified OpenAPI document
        if ($Server.OpenApiDocumentDescriptor.Count -gt 0 ) {
            $docDescriptor = $Server.DefaultOpenApiDocumentDescriptor

            $example = switch ($PSCmdlet.ParameterSetName) {
                'Value' {
                    $docDescriptor.NewOpenApiExample(
                        $Summary,
                        $Description,
                        $Value,
                        $Extensions
                    )
                }
                'DataValue' {
                    $docDescriptor.NewOpenApiExample(
                        $Summary,
                        $Description,
                        $DataValue,
                        $SerializedValue,
                        $Extensions
                    )
                }
                'ExternalValue' {
                    $docDescriptor.NewOpenApiExternalExample(
                        $Summary,
                        $Description,
                        $ExternalValue,
                        $Extensions
                    )
                }
            }

            return $example
        }
    }
}
