<#
.SYNOPSIS
    Tests the OpenAPI document for the specified Kestrun server.
.DESCRIPTION
    This function tests the OpenAPI document for the specified Kestrun server using the discovered components.
.PARAMETER Server
    The Kestrun server instance for which the OpenAPI document will be tested.
    If not specified, the function will attempt to resolve the current server context.
.PARAMETER DocId
    The ID of the OpenAPI document to test. Default is 'default'.
.PARAMETER Version
    The OpenAPI specification version to use for testing. Default is OpenApi3_1.
.PARAMETER ThrowOnError
    If specified, the function will throw an error if the OpenAPI document contains any validation errors
.PARAMETER DiagnosticVariable
    If specified, the function will store the diagnostic results in a variable with this name in the script scope.
.EXAMPLE
    # Test the OpenAPI document for the default document ID
    Test-KrOpenApiDocument -Server $myServer -DocId 'default'
.OUTPUTS
    Microsoft.OpenApi.Reader.OpenApiDiagnostic
#>
function Test-KrOpenApiDocument {
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([Microsoft.OpenApi.Reader.OpenApiDiagnostic])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string]$DocId = 'default',
        [Parameter(Mandatory = $false)]
        [Microsoft.OpenApi.OpenApiSpecVersion]$Version = [Microsoft.OpenApi.OpenApiSpecVersion]::OpenApi3_1,
        [switch]$ThrowOnError,
        [string]$DiagnosticVariable
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ( -not $Server.OpenApiDocumentDescriptor.ContainsKey($DocId)) {
            throw "OpenAPI document with ID '$DocId' does not exist on the server."
        }
        # Log the start of the validation process
        Write-KrLog -Level Information -Message "Starting OpenAPI document validation for DocId: '{DocId}' Version: '{Version}'" -Values $DocId, $Version
        # Retrieve the document descriptor
        $doc = $Server.OpenApiDocumentDescriptor[$DocId]
        # Read and diagnose the OpenAPI document
        $result = $doc.ReadAndDiagnose($Version)
        if ($null -eq $result) {
            throw "Failed to read and diagnose the OpenAPI document with ID '$DocId'."
        }
        # Log the diagnostic results
        foreach ($diagnosticWarning in $result.Diagnostic.Warnings) {
            Write-KrLog -Level Warning -Message $diagnosticWarning.ToString()
        }
        foreach ($diagnosticError in $result.Diagnostic.Errors) {
            Write-KrLog -Level Error -Message $diagnosticError.ToString()
        }
        if ($result.Diagnostic.Errors.Count -eq 0 -and $result.Diagnostic.Warnings.Count -eq 0) {
            Write-KrLog -Level Information -Message 'OpenAPI document validation completed successfully.'
        } elseif ($result.Diagnostic.Errors.Count -eq 0) {
            Write-KrLog -Level Warning -Message 'OpenAPI document validation completed with {WarningsCount} warnings.' -Values $result.Diagnostic.Warnings.Count
        } else {
            Write-KrLog -Level Error -Message 'OpenAPI document validation completed  with {ErrorsCount} errors.' -Values $result.Diagnostic.Errors.Count
        }
        # Throw an error if validation failed and the switch is set
        if ($ThrowOnError.IsPresent -and $result.Diagnostic.Errors.Count -gt 0) {
            $errorMessages = $result.Diagnostic.Errors | ForEach-Object { $_.Message }
            $combinedMessage = "OpenAPI document validation failed with the following errors:`n" + ($errorMessages -join "`n")
            throw $combinedMessage
        }
        if ($DiagnosticVariable) {
            Set-Variable -Name $DiagnosticVariable -Value $result.Diagnostic -Scope Script -Force
        }
        return ($result.Diagnostic.Errors.Count -eq 0 -and $result.Diagnostic.Warnings.Count -eq 0)
    }
}
