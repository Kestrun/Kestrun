<#
.SYNOPSIS
    Gets all OpenAPI schema types from the loaded assemblies.
.DESCRIPTION
    This function scans the loaded assemblies for classes marked with OpenAPI schema attributes
    and returns a list of those types, including any complex types they reference.
.PARAMETER HintType
    Optional type to restrict the scan to its assembly (e.g., [Address]).
.EXAMPLE
    Get-KrOpenApiSchemaTypes
    Retrieves all OpenAPI schema types from all loaded assemblies.
.EXAMPLE
    Get-KrOpenApiSchemaTypes -HintType [UserInfoResponse]
    Retrieves OpenAPI schema types from the assembly containing UserInfoResponse.
#>
function Get-KrOpenApiSchema {
    param(
        [Type]$HintType  # optional: restrict scan to this assembly (e.g., [Address])
    )

    # 1) pick assemblies
    $assemblies = if ($HintType) { @($HintType.Assembly) } else { [AppDomain]::CurrentDomain.GetAssemblies() }

    # 2) fetch all non-abstract classes from those assemblies
    $all = $assemblies | ForEach-Object { $_.GetTypes() } | Where-Object { $_.IsClass -and -not $_.IsAbstract }

    # helpers
    function Test-IsSchemaKind([Type]$t) {
        foreach ($a in $t.GetCustomAttributes($true)) {
            if ($a.GetType().Name -eq 'OpenApiModelKindAttribute' -and ($a.PSObject.Properties.Name -contains 'Kind')) {
                if ($a.Kind -eq [OpenApiModelKind]::Schema) { return $true }
            }
        }
        return $false
    }
    function Test-HasClassSchema([Type]$t) {
        foreach ($a in $t.GetCustomAttributes($true)) {
            if ($a.GetType().Name -eq 'OpenApiSchemaAttribute') { return $true }
        }
        return $false
    }
    function Get-PublicPropTypes([Type]$t) {
        $flags = [System.Reflection.BindingFlags] 'Public,Instance'
        foreach ($p in $t.GetProperties($flags)) {
            $pt = $p.PropertyType
            if ($pt.IsArray) { $pt = $pt.GetElementType() }
            if ($pt) { $pt }
        }
    }
    function Is-PrimitiveLike([Type]$t) {
        return $t.IsPrimitive -or
        $t -eq [string] -or
        $t -eq [decimal] -or
        $t -eq [datetime] -or
        $t -eq [guid]
    }

    # 3) seed set: explicitly marked schema classes OR have a class-level OpenApiSchema
    $seed = $all | Where-Object { (Test-IsSchemaKind $_) -or (Test-HasClassSchema $_) }

    # 4) expand transitively: pull in referenced complex types + enums
    $seen = [System.Collections.Generic.HashSet[Type]]::new()
    $queue = [System.Collections.Generic.Queue[Type]]::new()
    foreach ($t in $seed) { [void]$seen.Add($t); $queue.Enqueue($t) }

    while ($queue.Count -gt 0) {
        $t = $queue.Dequeue()
        foreach ($pt in Get-PublicPropTypes $t) {
            if ($pt.IsEnum) {
                [void]$seen.Add($pt) | Out-Null
                continue
            }
            if (-not (Is-PrimitiveLike $pt) -and -not $pt.IsEnum -and $pt.IsClass -and -not $pt.IsAbstract) {
                if ($seen.Add($pt)) { $queue.Enqueue($pt) }
            }
        }
    }

    # 5) return unique list: schemas + referenced enums
    $seen | Where-Object { $_ } | Sort-Object FullName
}
