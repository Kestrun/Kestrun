function Get-KrAnnotatedFunctionsLoaded {
    [CmdletBinding()]
    param(
    )

    # All loaded functions now in the runspace
    $funcs = @(Get-Command -CommandType Function | Where-Object {
            $null -eq $_.Module -and $null -eq $_.PsDrive
        })



    foreach ($cmd in $funcs) {
        $routeOptions = [Kestrun.Hosting.Options.MapRouteOptions]::new()
        foreach ($attr in $cmd.ScriptBlock.Attributes) {
            if ($attr -is [OpenApiPath]) {
                if (-not [string]::IsNullOrWhiteSpace($attr.HttpVerb)) {
                    $verb = [Kestrun.Utilities.HttpVerbExtensions]::FromMethodString($attr.HttpVerb)
                    $routeOptions.HttpVerbs += $verb
                    if (-not $routeOptions.OpenApi.ContainsKey($verb)) {
                        $routeOptions.OpenApi[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($routeOptions.Pattern)
                    }
                }
                if (-not [string]::IsNullOrWhiteSpace($attr.Pattern)) {
                    $routeOptions.Pattern = $attr.Pattern
                }
                if (-not [string]::IsNullOrWhiteSpace($attr.Summary)) {
                    $routeOptions.Summary = $attr.Summary
                }
                if (-not [string]::IsNullOrWhiteSpace($attr.Description)) {
                    $routeOptions.Description = $attr.Description
                }
                if ($attr.Tags -and $attr.Tags.Count -gt 0) {
                    $routeOptions.Tags += $attr.Tags
                }
                if (-not [string]::IsNullOrWhiteSpace($attr.OperationId)) {
                    $routeOptions.OperationId = $attr.OperationId
                }

                $routeOptions.OpenApi[$verb].Deprecated = $attr.Deprecated
                $routeOptions.OpenApi[$verb].Enabled = $true
            }
        }
        if ([string]::IsNullOrWhiteSpace($routeOptions.Pattern)) {
            $routeOptions.Pattern = '/' + $cmd.Name
        }
        $routeOptions.ScriptCode.Code = $cmd.ScriptBlock.ToString()
        $routeOptions.ScriptCode.language = [Kestrun.Scripting.ScriptLanguage]::PowerShell

        Add-KrMapRoute -Options $routeOptions


        <#            -or
               $attr -is [OpenApiOperation] -or
               $attr -is [OpenApiParameter] -or
               $attr -is [OpenApiRequestBody] -or
               $attr -is [OpenApiResponse] -or
               $attr -is [OpenApiResponseRef] -or
               $attr -is [OpenApiSchema] -or
               $attr -is [OpenApiSchemaProperty] ) {
                continue 2
            }
            if ($isOpenApi.Invoke($attr)) {
                continue 2
            }#>
    }

}
