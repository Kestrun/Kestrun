function New-KrOpenApiSchemasJson {
    param(
        [string]$Title = 'Kestrun API',
        [string]$Version = '1.0.0',
        [Type]$HintType  # optional: e.g. ([Address]) to limit scan
    )
    <#   $types = Get-KrOpenApiSchema -HintType $HintType
    if (-not $types -or $types.Count -eq 0) {
        throw 'No schema types discovered. Make sure your classes are defined and annotated.'
    }

    $typed = [System.Collections.Generic.List[Type]]::new()
    foreach ($t in $types) {
        if ($t) { [void]$typed.Add([Type]$t) }
    }
#>
    # Auto-discover all schema types (optionally restrict with a hint)
    $types = [Kestrun.OpenApi.OpenApiSchemaDiscovery]::GetOpenApiSchemaTypes([Address])

    # call your C# v2 generator (schemas only)
    $doc = [Kestrun.OpenApi.OpenApiV2Generator]::Generate($types, $Title, $Version)
    $json = [Kestrun.OpenApi.OpenApiV2Generator]::ToJson($doc, $true) # 3.1
    return $json
}
