<#
.SYNOPSIS

.DESCRIPTION
.PARAMETER Server
    The Kestrun server instance (resolved if omitted via Resolve-KestrunServer).
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
        [switch] $IncludeDetailsInDevelopment,
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

        [switch] $PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $exceptionOptions = [Kestrun.Hosting.Options.ExceptionOptions]::new($Server)
        switch ($PSCmdlet.ParameterSetName) {

            'LanguageOptions' {
                $exceptionOptions.LanguageOptions = $LanguageOptions
            }
            default {
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
        }
        # Assign the configured options to the server instance
        $Server.ExceptionOptions = $exceptionOptions

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
