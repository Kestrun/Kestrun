<#
.SYNOPSIS
    Enables exception handling middleware for a Kestrun server instance.
.DESCRIPTION
    This cmdlet configures the exception handling middleware for a Kestrun server instance,
    allowing for customizable error handling and response generation.
.PARAMETER Server
    The Kestrun server instance (resolved if omitted via Resolve-KestrunServer).
.PARAMETER ExceptionHandlingPath
    The path to re-execute when an exception occurs (e.g., '/error').
.PARAMETER CreateScopeForErrors
    If specified, creates a new scope for handling errors.
.PARAMETER AllowStatusCode404Response
    If specified, allows handling of 404 status code responses.
.PARAMETER IncludeDetailsInDevelopment
    If specified, includes detailed error information when the environment is set to Development.
.PARAMETER UseProblemDetails
    If specified, formats error responses using the Problem Details standard (RFC 7807).
.PARAMETER LanguageOptions
    A LanguageOptions object defining the scripting language, code, references, imports, and arguments
    for custom error handling logic.
.PARAMETER ScriptBlock
    A PowerShell script block to execute for custom error handling logic.
.PARAMETER Code
    A string containing the code to execute for custom error handling logic.
.PARAMETER Language
    The scripting language of the provided code (e.g., PowerShell, CSharp, VisualBasic).
.PARAMETER CodeFilePath
    The file path to a script file containing the code to execute for custom error handling logic.
.PARAMETER ExtraRefs
    An array of additional assemblies to reference when executing the custom error handling code.
.PARAMETER Arguments
    A hashtable of arguments to pass to the custom error handling code.
.PARAMETER ExtraImports
    An array of additional namespaces to import when executing the custom error handling code.
.PARAMETER PassThru
    If specified, returns the modified Kestrun server instance.
.OUTPUTS
    The modified Kestrun server instance if the PassThru parameter is specified; otherwise, no output.
.NOTES
    This function is part of the Kestrun PowerShell module and is used to manage Kestrun servers
    and their middleware components.
#>
function Enable-KrExceptionHandling {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost] $Server,

        # Redirect
        [string] $ExceptionHandlingPath,

        [switch] $CreateScopeForErrors,
        [switch] $AllowStatusCode404Response,
        [Parameter(ParameterSetName = 'Json')]
        [switch] $IncludeDetailsInDevelopment,
        [Parameter(ParameterSetName = 'Json')]
        [switch] $UseProblemDetails,

        # Custom via LanguageOptions or ScriptBlock
        [Parameter(ParameterSetName = 'LanguageOptions')]
        [Kestrun.Hosting.Options.LanguageOptions] $LanguageOptions,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [scriptblock] $ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [Alias('CodeBlock')]
        [string]$Code,
        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [Kestrun.Scripting.ScriptLanguage]$Language,

        [Parameter(Mandatory = $true, ParameterSetName = 'CodeFilePath')]
        [string]$CodeFilePath,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [System.Reflection.Assembly[]]$ExtraRefs = $null,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [hashtable]$Arguments,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [string[]]$ExtraImports = $null,

        [switch] $PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $exceptionOptions = [Kestrun.Hosting.Options.ExceptionOptions]::new($Server)

        # Map direct ExceptionOptions properties from parameters
        if ($PSBoundParameters.ContainsKey('ExceptionHandlingPath')) {
            $exceptionOptions.ExceptionHandlingPath = [Microsoft.AspNetCore.Http.PathString]::new($ExceptionHandlingPath)
        }
        if ($PSBoundParameters.ContainsKey('CreateScopeForErrors')) {
            $exceptionOptions.CreateScopeForErrors = $CreateScopeForErrors.IsPresent
        }
        if ($PSBoundParameters.ContainsKey('AllowStatusCode404Response')) {
            $exceptionOptions.AllowStatusCode404Response = $AllowStatusCode404Response.IsPresent
        }
        if ($PSCmdlet.ParameterSetName -eq 'Json') {
            # If using the JSON fallback, set up the built-in JSON handler
            $exceptionOptions.UseJsonExceptionHandler($UseProblemDetails.IsPresent, $IncludeDetailsInDevelopment.IsPresent)
        } elseif ($PSCmdlet.ParameterSetName -eq 'LanguageOptions') {
            $exceptionOptions.LanguageOptions = $LanguageOptions
        } elseif ($PSCmdlet.ParameterSetName -ne 'Default') {
            $lo = [Kestrun.Hosting.Options.LanguageOptions]::new()
            $lo.ExtraImports = $ExtraImports
            $lo.ExtraRefs = $ExtraRefs
            if ($null -ne $Arguments) {
                $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
                foreach ($key in $Arguments.Keys) {
                    $dict[$key] = $Arguments[$key]
                }
                $lo.Arguments = $dict
            }

            switch ($PSCmdlet.ParameterSetName) {
                'ScriptBlock' {
                    $lo.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                    $lo.Code = $ScriptBlock.ToString()
                }
                'Code' {
                    $lo.Language = $Language
                    $lo.Code = $Code
                }
                'CodeFilePath' {
                    if (-not (Test-Path -Path $CodeFilePath)) {
                        throw "The specified code file path does not exist: $CodeFilePath"
                    }
                    $extension = Split-Path -Path $CodeFilePath -Extension
                    switch ($extension) {
                        '.ps1' {
                            $lo.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                        }
                        '.cs' {
                            $lo.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                        }
                        '.vb' {
                            $lo.Language = [Kestrun.Scripting.ScriptLanguage]::VisualBasic
                        }
                        default {
                            throw "Unsupported '$extension' code file extension."
                        }
                    }
                    $lo.Code = Get-Content -Path $CodeFilePath -Raw
                }
                default {
                    throw "Unrecognized ParameterSetName: $($PSCmdlet.ParameterSetName)"
                }
            }
            $exceptionOptions.LanguageOptions = $lo
        }


        # Assign the configured options to the server instance
        $Server.ExceptionOptions = $exceptionOptions

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
