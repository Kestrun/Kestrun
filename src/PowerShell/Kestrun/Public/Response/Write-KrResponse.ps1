<#
    .SYNOPSIS
        Writes a response with the specified input object and HTTP status code.
    .DESCRIPTION
        This function is a wrapper around the Kestrun server response methods.
        The response format based on the Accept header or defaults to text/plain.
        Content type is determined automatically.
    .PARAMETER InputObject
        The input object to write to the response body.
    .PARAMETER StatusCode
        The HTTP status code to set for the response. Defaults to 200 (OK).

    .EXAMPLE
        Write-KrResponse -InputObject $myObject -StatusCode 200
        Writes the $myObject to the response with a 200 status code. The content type
        is determined automatically based on the Accept header or defaults to text/plain.
    .NOTES
        This function is designed to be used in the context of a Kestrun server response.
#>
function Write-KrResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter()]
        [int]$StatusCode = 200
    )

    function Convert-KrSchemaValue {
        param(
            [AllowNull()]
            [object]$Value,
            [Parameter(Mandatory = $true)]
            [type]$TargetType
        )

        function Test-KrMapLikeType {
            param(
                [Parameter(Mandatory = $true)]
                [type]$Type
            )

            $flags = [System.Reflection.BindingFlags]'Public,Instance,Static,FlattenHierarchy'
            if ($null -ne $Type.GetProperty('AdditionalProperties', [System.Reflection.BindingFlags]'Public,Instance,FlattenHierarchy')) {
                return $true
            }

            foreach ($attribute in $Type.GetCustomAttributes($true)) {
                $attributeName = $attribute.GetType().Name
                if ($attributeName -eq 'OpenApiPatternPropertiesAttribute') {
                    return $true
                }
            }

            return $false
        }

        if ($null -eq $Value) {
            return $null
        }

        $valueType = $Value.GetType()
        if ($TargetType.IsInstanceOfType($Value) -or $valueType -eq $TargetType) {
            return $Value
        }

        if ($TargetType.IsGenericType -and $TargetType.GetGenericTypeDefinition() -eq [Nullable``1]) {
            $innerType = $TargetType.GetGenericArguments()[0]
            return Convert-KrSchemaValue -Value $Value -TargetType $innerType
        }

        $targetIsMapLike = Test-KrMapLikeType -Type $TargetType
        if ($targetIsMapLike -and $Value -is [System.Collections.IDictionary]) {
            return $Value
        }

        if ($TargetType.IsArray) {
            $elementType = $TargetType.GetElementType()
            if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
                $items = @()
                foreach ($item in $Value) {
                    $items += , (Convert-KrSchemaValue -Value $item -TargetType $elementType)
                }

                $typedArray = [System.Array]::CreateInstance($elementType, $items.Count)
                for ($index = 0; $index -lt $items.Count; $index++) {
                    $typedArray.SetValue($items[$index], $index)
                }

                return $typedArray
            }

            $singleItemArray = [System.Array]::CreateInstance($elementType, 1)
            $singleItemArray.SetValue((Convert-KrSchemaValue -Value $Value -TargetType $elementType), 0)
            return $singleItemArray
        }

        $declaredProperties = $TargetType.GetProperties([System.Reflection.BindingFlags]'Public,Instance,DeclaredOnly')
        if (
            $declaredProperties.Count -eq 0 -and
            $Value -is [System.Collections.IEnumerable] -and
            -not ($Value -is [string]) -and
            -not ($Value -is [System.Collections.IDictionary])
        ) {
            return $Value
        }

        if (
            $declaredProperties.Count -eq 0 -and
            $Value -is [System.Collections.IDictionary]
        ) {
            return $Value
        }

        $schemaComponentAttribute = $TargetType.GetCustomAttributes($true) |
            Where-Object { $_.GetType().Name -eq 'OpenApiSchemaComponentAttribute' } |
            Select-Object -First 1

        if (
            $null -ne $schemaComponentAttribute -and
            $schemaComponentAttribute.Array -and
            $Value -is [System.Collections.IEnumerable] -and
            -not ($Value -is [string]) -and
            -not ($Value -is [System.Collections.IDictionary])
        ) {
            return $Value
        }

        $singleArgConstructors = $TargetType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 1 }
        foreach ($constructor in $singleArgConstructors) {
            try {
                $parameterType = $constructor.GetParameters()[0].ParameterType
                $convertedArg = [System.Management.Automation.LanguagePrimitives]::ConvertTo($Value, $parameterType, [System.Globalization.CultureInfo]::InvariantCulture)
                return $constructor.Invoke(@($convertedArg))
            } catch {
            }
        }

        if ($Value -is [System.Collections.IDictionary]) {
            $defaultConstructor = $TargetType.GetConstructor([Type[]]@())
            if ($null -ne $defaultConstructor) {
                $instance = $defaultConstructor.Invoke(@())
                $properties = $TargetType.GetProperties([System.Reflection.BindingFlags]'Public,Instance') |
                    Where-Object { $_.CanWrite }

                foreach ($property in $properties) {
                    $matchKey = $null
                    foreach ($candidateKey in $Value.Keys) {
                        if ([string]::Equals([string]$candidateKey, $property.Name, [System.StringComparison]::OrdinalIgnoreCase)) {
                            $matchKey = $candidateKey
                            break
                        }
                    }

                    if ($null -ne $matchKey) {
                        $rawPropertyValue = $Value[$matchKey]
                        if (
                            $rawPropertyValue -is [System.Collections.IDictionary] -and
                            (Test-KrMapLikeType -Type $property.PropertyType)
                        ) {
                            return $Value
                        }
                    }
                }

                foreach ($property in $properties) {
                    $matchKey = $null
                    foreach ($candidateKey in $Value.Keys) {
                        if ([string]::Equals([string]$candidateKey, $property.Name, [System.StringComparison]::OrdinalIgnoreCase)) {
                            $matchKey = $candidateKey
                            break
                        }
                    }

                    if ($null -ne $matchKey) {
                        $rawPropertyValue = $Value[$matchKey]
                        $convertedPropertyValue = Convert-KrSchemaValue -Value $rawPropertyValue -TargetType $property.PropertyType
                        try {
                            $property.SetValue($instance, $convertedPropertyValue)
                        } catch {
                            if (
                                $property.PropertyType.IsArray -and
                                -not ($convertedPropertyValue -is [System.Array])
                            ) {
                                $arrayType = $property.PropertyType
                                $elementType = $arrayType.GetElementType()
                                $typedArray = [System.Array]::CreateInstance($elementType, 1)
                                $typedArray.SetValue((Convert-KrSchemaValue -Value $convertedPropertyValue -TargetType $elementType), 0)
                                $property.SetValue($instance, $typedArray)
                            } else {
                                throw
                            }
                        }
                    }
                }

                return $instance
            }
        }

        return [System.Management.Automation.LanguagePrimitives]::ConvertTo($Value, $TargetType, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    function Resolve-KrSchemaType {
        param(
            [Parameter(Mandatory = $true)]
            [string]$SchemaTypeName
        )

        $candidates = [System.Collections.Generic.List[type]]::new()

        $directType = [type]::GetType($SchemaTypeName, $false, $true)
        if ($null -ne $directType) {
            $candidates.Add($directType)
        }

        foreach ($assembly in [AppDomain]::CurrentDomain.GetAssemblies()) {
            $assemblyType = $assembly.GetType($SchemaTypeName, $false, $true)
            if ($null -ne $assemblyType) {
                $candidates.Add($assemblyType)
            }

            $typeCandidates = @()
            try {
                $typeCandidates = $assembly.GetTypes()
            } catch [System.Reflection.ReflectionTypeLoadException] {
                $typeCandidates = @($_.Exception.Types | Where-Object { $null -ne $_ })
            } catch {
                continue
            }

            foreach ($typeCandidate in $typeCandidates) {
                if (
                    [string]::Equals($typeCandidate.FullName, $SchemaTypeName, [System.StringComparison]::OrdinalIgnoreCase) -or
                    [string]::Equals($typeCandidate.Name, $SchemaTypeName, [System.StringComparison]::OrdinalIgnoreCase) -or
                    ($null -ne $typeCandidate.AssemblyQualifiedName -and $typeCandidate.AssemblyQualifiedName -like "$SchemaTypeName,*")
                ) {
                    $candidates.Add($typeCandidate)
                }
            }
        }

        $distinct = [System.Collections.Generic.List[type]]::new()
        $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($candidate in $candidates) {
            if ($null -eq $candidate) {
                continue
            }

            $key = if (-not [string]::IsNullOrWhiteSpace($candidate.AssemblyQualifiedName)) {
                $candidate.AssemblyQualifiedName
            } else {
                $candidate.FullName
            }

            if ($seen.Add($key)) {
                $distinct.Add($candidate)
            }
        }

        if ($distinct.Count -eq 0) {
            return $null
        }

        $generatedCandidate = $distinct |
            Where-Object {
                $null -ne $_.GetProperty('XmlMetadata', [System.Reflection.BindingFlags]'Public,Static,FlattenHierarchy')
            } |
            Select-Object -First 1

        if ($null -ne $generatedCandidate) {
            return $generatedCandidate
        }

        return $distinct[0]
    }

    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        Write-KrLog -Level Debug -Message "Write-KrResponse invoked for status code {statusCode}. Input type: {inputType}" -Values $StatusCode, ($InputObject.GetType().FullName)

        if ($Context.MapRouteOptions.DefaultResponseContentType.ContainsKey($StatusCode)) {
            $responseTypeMappings = $Context.MapRouteOptions.DefaultResponseContentType[$StatusCode]
            Write-KrLog -Level Debug -Message "Found {mappingCount} response type mapping(s) for status code {statusCode}." -Values $responseTypeMappings.Count, $StatusCode

            if ($null -ne $responseTypeMappings[0].Schema) {
                $schema = $responseTypeMappings[0].Schema
                Write-KrLog -Level Debug -Message "Schema metadata found for status code {statusCode}. Schema value type: {schemaValueType}." -Values $StatusCode, ($schema.GetType().FullName)
                $inputType = $InputObject.GetType()

                $schemaType = $null
                $schemaTypeName = $null
                if ($schema -is [string] -and -not [string]::IsNullOrWhiteSpace($schema)) {
                    $schemaTypeName = $schema
                    Write-KrLog -Level Debug -Message "Resolving schema type from name '{schemaTypeName}'." -Values $schemaTypeName
                    $schemaType = Resolve-KrSchemaType -SchemaTypeName $schemaTypeName

                    if ($null -ne $schemaType) {
                        Write-KrLog -Level Debug -Message "Schema type name '{schemaTypeName}' resolved to {resolvedType}." -Values $schemaTypeName, $schemaType.FullName
                    }

                    $namesMatch =
                    $inputType.FullName -eq $schemaTypeName -or
                    $inputType.Name -eq $schemaTypeName -or
                    $inputType.AssemblyQualifiedName -like "$schemaTypeName,*"

                    if ($namesMatch -and ($null -eq $schemaType -or $inputType -ne $schemaType)) {
                        Write-KrLog -Level Debug -Message "Schema type name '{schemaTypeName}' matched input runtime type {inputType}; using input runtime type for validation/conversion." -Values $schemaTypeName, $inputType.FullName
                        $schemaType = $inputType
                    }
                } else {
                    Write-KrLog -Level Error -Message "Schema value for status code {statusCode} must be a non-empty type name string, but found {schemaValueType}." -Values $StatusCode, ($schema.GetType().FullName)
                    $Context.Response.PostPonedWriteObject.Error = 500
                }

                if ($null -eq $schemaType) {
                    Write-KrLog -Level Error -Message "Unable to resolve response schema type '{schemaTypeName}' for status code {statusCode}." -Values $schema, $StatusCode
                    $Context.Response.PostPonedWriteObject.Error = 500
                } else {
                    try {
                        $valueToWrite = $null
                        if ($schemaType.IsInstanceOfType($InputObject) -or $inputType -eq $schemaType) {
                            Write-KrLog -Level Debug -Message "Input object already matches schema type {schemaTypeName}; conversion skipped." -Values $schemaType.FullName
                            $valueToWrite = $InputObject
                        } else {
                            Write-KrLog -Level Debug -Message "Converting input object from {inputType} to schema type {schemaTypeName}." -Values $inputType.FullName, $schemaType.FullName
                            $converted = Convert-KrSchemaValue -Value $InputObject -TargetType $schemaType
                            Write-KrLog -Level Debug -Message "Conversion successful. Converted type: {convertedType}." -Values ($converted.GetType().FullName)
                            $valueToWrite = $converted
                        }

                        if ($null -ne $valueToWrite -and $null -ne $valueToWrite.PSObject.Methods['ValidateRequiredProperties']) {
                            Write-KrLog -Level Debug -Message "ValidateRequiredProperties() found on schema type {schemaTypeName}; validating required properties." -Values $schemaType.FullName
                            try {
                                $isValid = $valueToWrite.ValidateRequiredProperties()
                                Write-KrLog -Level Debug -Message "ValidateRequiredProperties() returned {isValid} for schema type {schemaTypeName}." -Values $isValid, $schemaType.FullName

                                if ($isValid -is [bool] -and -not $isValid) {
                                    $missingProperties = @()
                                    if ($null -ne $valueToWrite.PSObject.Methods['GetMissingRequiredProperties']) {
                                        $missingProperties = @($valueToWrite.GetMissingRequiredProperties())
                                    }

                                    $missingText = if ($missingProperties.Count -gt 0) {
                                        ($missingProperties -join ', ')
                                    } else {
                                        'unknown required properties'
                                    }

                                    Write-KrLog -Level Error -Message "Response object failed required-property validation for schema type {schemaTypeName}. Missing: {missingProperties}." -Values $schemaType.FullName, $missingText
                                    $Context.Response.PostPonedWriteObject.Error = 500
                                }
                            } catch {
                                Write-KrLog -Level Error -Message "Error while validating required properties for schema type {schemaTypeName}. Error: {error}" -Values $schemaType.FullName, $_.Exception.Message
                                $Context.Response.PostPonedWriteObject.Error = 500
                            }
                        }

                        if ($Context.Response.PostPonedWriteObject.Error -eq $null -or $Context.Response.PostPonedWriteObject.Error -eq 0) {
                            $Context.Response.PostPonedWriteObject.Value = $valueToWrite
                        }
                    } catch {
                        Write-KrLog -Level Error -Message "Failed to convert response object to schema type '{schemaTypeName}' for status code {statusCode}. Error: {error}" -Values $schemaType.FullName, $StatusCode, $_.Exception.Message
                        $Context.Response.PostPonedWriteObject.Error = 500
                    }
                }
            } else {
                Write-KrLog -Level Debug -Message "No schema metadata configured for status code {statusCode}; using original input object." -Values $StatusCode
                $Context.Response.PostPonedWriteObject.Value = $InputObject
            }
        } else {
            Write-KrLog -Level Debug -Message "No response schema mapping defined for status code {statusCode}; using original input object." -Values $StatusCode
            $Context.Response.PostPonedWriteObject.Value = $InputObject
        }

        $Context.Response.PostPonedWriteObject.Status = $StatusCode
        # Call the C# method on the $Context.Response object
        #$Context.Response.WriteResponse($InputObject, $StatusCode)
    } else {
        Write-KrOutsideRouteWarning
    }
}

