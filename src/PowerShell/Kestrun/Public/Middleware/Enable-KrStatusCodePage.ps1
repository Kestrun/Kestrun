<#
.SYNOPSIS
    Enables Status Code Pages for a Kestrun server.
.DESCRIPTION
    Wraps KestrunHostStatusCodePagesExtensions to configure how 4xx–5xx responses
    produce a body: default text, redirect, re-execute, template body, options object,
    or a scripted handler via LanguageOptions (PowerShell/C#).
.PARAMETER Server
    The Kestrun server instance (resolved if omitted via Resolve-KestrunServer).
.PARAMETER Location
    The location URL for Redirect mode (e.g. "/error/{0}").
.PARAMETER Path
    The path to re-execute for ReExecute mode (e.g. "/error/{0}").
.PARAMETER Query
    The query string to append for ReExecute mode (e.g. "?code={0}").
.PARAMETER ContentType
    The content type for Template mode (e.g. "text/plain" or "application/json").
.PARAMETER BodyFormat
    The body format string for Template mode (e.g. "Oops {0}").
.PARAMETER LanguageOptions
    A pre-configured LanguageOptions object for custom scripted handling.
.PARAMETER ScriptBlock
    A PowerShell script block for custom scripted handling. The script block
    receives a single parameter: the HttpContext object.
.PARAMETER Code
    A code string for custom scripted handling.
.PARAMETER Language
    The scripting language for the code string (PowerShell, CSharp, VisualBasic).
.PARAMETER CodeFilePath
    A file path to a code file for custom scripted handling (.ps1, .cs, .vb).
.PARAMETER ExtraRefs
    Additional assembly references for custom scripted handling.
.PARAMETER Arguments
    Additional arguments (key-value pairs) for custom scripted handling.
.PARAMETER PassThru
    If specified, the function will return the created route object.
.EXAMPLE
    Enable-KrStatusCodePage -Mode Default
.EXAMPLE
    Enable-KrStatusCodePage -Mode Redirect -Location "/error/{0}"
.EXAMPLE
    Enable-KrStatusCodePage -Mode ReExecute -Path "/error/{0}" -Query "?code={0}"
.EXAMPLE
    Enable-KrStatusCodePage -Mode Template -ContentType "text/plain" -BodyFormat "Oops {0}"
.EXAMPLE
    $opts = [Microsoft.AspNetCore.Diagnostics.StatusCodePagesOptions]::new()
    Enable-KrStatusCodePage -Mode Options -Options $opts
.EXAMPLE
    $lo = [Kestrun.Hosting.Options.LanguageOptions]::new()
    $lo.ExtraImports = @('System.Net')
    $lo.Code = {
        param($Context)
        $statusCode = $Context.Response.StatusCode
        $reasonPhrase = [System.Net.HttpStatusCode]::Parse([int]$statusCode).ToString()
        $message = "Custom handler: Status code $statusCode - $reasonPhrase"
        $Context.Response.ContentType = 'text/plain'
        $Context.Response.WriteAsync($message) | Out-Null
    }
    Enable-KrStatusCodePage -Mode LanguageOptions -LanguageOptions $lo
.EXAMPLE
    Enable-KrStatusCodePage -Mode ScriptBlock -ScriptBlock {
        param($Context)
        $statusCode = $Context.Response.StatusCode
        $reasonPhrase = [System.Net.HttpStatusCode]::Parse([int]$statusCode).ToString()
        $message = "Custom handler: Status code $statusCode - $reasonPhrase"
        $Context.Response.ContentType = 'text/plain'
        $Context.Response.WriteAsync($message) | Out-Null
    }
.EXAMPLE
    Enable-KrStatusCodePage -Mode Code -Language PowerShell -Code {
        param($Context)
        $statusCode = $Context.Response.StatusCode
        $reasonPhrase = [System.Net.HttpStatusCode]::Parse([int]$statusCode).ToString()
        $message = "Custom handler: Status code $statusCode - $reasonPhrase"
        $Context.Response.ContentType = 'text/plain'
        $Context.Response.WriteAsync($message) | Out-Null
    }
.EXAMPLE
    Enable-KrStatusCodePage -Mode CodeFilePath -CodeFilePath 'C:\Scripts\StatusCodeHandler.ps1'
.NOTES
    This function is part of the Kestrun PowerShell module and is used to manage Kestrun servers
    and their middleware components.
#>
function Enable-KrStatusCodePage {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost] $Server,

        # Redirect
        [Parameter(Mandatory = $true, ParameterSetName = 'Redirect')]
        [string] $Location,

        # Re-execute
        [Parameter(Mandatory = $true, ParameterSetName = 'ReExecute')]
        [string] $Path,
        [Parameter(ParameterSetName = 'ReExecute')]
        [string] $Query,

        # Template body
        [Parameter(Mandatory = $true, ParameterSetName = 'Template')]
        [string] $ContentType,
        [Parameter(Mandatory = $true, ParameterSetName = 'Template')]
        [string] $BodyFormat,

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
        if (-not $Server) { throw 'Kestrun server instance could not be resolved.' }
    }
    process {
        switch ($PSCmdlet.ParameterSetName) {
            'Default' {
                [Kestrun.Hosting.KestrunHostStatusCodePagesExtensions]::UseStatusCodePages($Server) | Out-Null
            }
            'Template' {
                if ([string]::IsNullOrWhiteSpace($ContentType)) { throw '-ContentType is required for Mode=Template.' }
                if ([string]::IsNullOrWhiteSpace($BodyFormat)) { throw '-BodyFormat is required for Mode=Template.' }
                [Kestrun.Hosting.KestrunHostStatusCodePagesExtensions]::UseStatusCodePages($Server, $ContentType, $BodyFormat) | Out-Null
            }
            'Redirect' {
                if ([string]::IsNullOrWhiteSpace($Location)) { throw '-Location is required for Mode=Redirect.' }
                [Kestrun.Hosting.KestrunHostStatusCodePagesExtensions]::UseStatusCodePagesWithRedirects($Server, $Location) | Out-Null
            }
            'ReExecute' {
                if ([string]::IsNullOrWhiteSpace($Path)) { throw '-Path is required for Mode=ReExecute.' }
                if ($PSBoundParameters.ContainsKey('Query')) {
                    [Kestrun.Hosting.KestrunHostStatusCodePagesExtensions]::UseStatusCodePagesWithReExecute($Server, $Path, $Query) | Out-Null
                } else {
                    [Kestrun.Hosting.KestrunHostStatusCodePagesExtensions]::UseStatusCodePagesWithReExecute($Server, $Path) | Out-Null
                }
            }
            'LanguageOptions' {
                # Custom via LanguageOptions
                [Kestrun.Hosting.KestrunHostStatusCodePagesExtensions]::UseStatusCodePages($Server, $LanguageOptions) | Out-Null
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
                # Register the status code pages middleware
                [Kestrun.Hosting.KestrunHostStatusCodePagesExtensions]::UseStatusCodePages($Server, $lo) | Out-Null
            }
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
