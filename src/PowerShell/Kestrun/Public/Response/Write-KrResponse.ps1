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
                    $schemaType = [type]::GetType($schemaTypeName, $false, $true)
                    if ($null -eq $schemaType) {
                        $schemaType = [AppDomain]::CurrentDomain.GetAssemblies() |
                            ForEach-Object { $_.GetType($schemaTypeName, $false, $true) } |
                            Where-Object { $null -ne $_ } |
                            Select-Object -First 1
                    }

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
                            $converted = [System.Management.Automation.LanguagePrimitives]::ConvertTo($InputObject, $schemaType, [System.Globalization.CultureInfo]::InvariantCulture)
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
            Write-KrLog -Level Error -Message "No Value type defined for status code {statusCode}" -Values $StatusCode
            $Context.Response.PostPonedWriteObject.Error = 500
        }

        $Context.Response.PostPonedWriteObject.Status = $StatusCode
        # Call the C# method on the $Context.Response object
        #$Context.Response.WriteResponse($InputObject, $StatusCode)
    } else {
        Write-KrOutsideRouteWarning
    }
}

