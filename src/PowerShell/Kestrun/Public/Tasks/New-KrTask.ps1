<#
.SYNOPSIS
    Creates a task without starting it.
.DESCRIPTION
    Returns a new task id after registering the code/file with the Task service.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Optional task id; if omitted, a new GUID is generated.
.PARAMETER AutoStart
    If specified, the task will be started immediately after creation.
.PARAMETER Name
    Optional human-friendly name of the task.
.PARAMETER Description
    Optional description of the task.
.PARAMETER Options
    Language options object; mutually exclusive with other code parameters.
.PARAMETER ScriptBlock
    PowerShell script block to run; mutually exclusive with other code parameters.
.PARAMETER Code
    Code string to run; mutually exclusive with other code parameters.
.PARAMETER Language
    Language of the code string; required when using -Code.
.PARAMETER CodeFilePath
    Path to a code file to run; mutually exclusive with other code parameters.
.PARAMETER ExtraImports
    Additional namespaces to import; applies to -Code and -CodeFilePath.
.PARAMETER ExtraRefs
    Additional assemblies to reference; applies to -Code and -CodeFilePath.
.PARAMETER Arguments
    Hashtable of named arguments to pass to the script; applies to -ScriptBlock, -Code, and -CodeFilePath.
.EXAMPLE
    New-KrTask -ScriptBlock { param($name) "Hello, $name!" } -Arguments @{ name = 'World' }

    Creates a new PowerShell task that greets the specified name.
.EXAMPLE
    New-KrTask -Code 'return 2 + 2' -Language CSharp
    Creates a new C# task that returns the result of 2 + 2.
.EXAMPLE
    New-KrTask -CodeFilePath 'C:\Scripts\MyScript.ps1'
    Creates a new PowerShell task from the specified script file.
.OUTPUTS
    Returns the id of the created task. The task is not started; use Start-KrTask to run it.
#>
function New-KrTask {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(DefaultParameterSetName = 'FromCode')]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$Id,

        [Parameter()]
        [switch]$AutoStart,

        [Parameter(Mandatory = $false)]
        [string]$Name,

        [parameter(Mandatory = $false)]
        [string]$Description,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Hosting.Options.LanguageOptions]$Options,

        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = 'ScriptBlock')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [Alias('CodeBlock')]
        [string]$Code,

        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [Kestrun.Scripting.ScriptLanguage]$Language,

        [Parameter(Mandatory = $true, ParameterSetName = 'CodeFilePath')]
        [string]$CodeFilePath,

        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [string[]]$ExtraImports = $null,

        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [System.Reflection.Assembly[]]$ExtraRefs = $null,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [hashtable]$Arguments
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Hosting.Options.LanguageOptions]::new()
            $Options.ExtraImports = $ExtraImports
            $Options.ExtraRefs = $ExtraRefs
            if ($null -ne $Arguments) {
                $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
                foreach ($key in $Arguments.Keys) {
                    $dict[$key] = $Arguments[$key]
                }
                $Options.Arguments = $dict
            }
            switch ($PSCmdlet.ParameterSetName) {
                'ScriptBlock' {
                    $Options.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                    $Options.Code = $ScriptBlock.ToString()
                }
                'Code' {
                    $Options.Language = $Language
                    $Options.Code = $Code
                }
                'CodeFilePath' {
                    if (-not (Test-Path -Path $CodeFilePath)) {
                        throw "The specified code file path does not exist: $CodeFilePath"
                    }
                    $extension = Split-Path -Path $CodeFilePath -Extension
                    switch ($extension) {
                        '.ps1' {
                            $Options.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                        }
                        '.cs' {
                            $Options.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                        }
                        '.vb' {
                            $Options.Language = [Kestrun.Scripting.ScriptLanguage]::VisualBasic
                        }
                        default {
                            throw "Unsupported '$extension' code file extension."
                        }
                    }
                    $Options.Code = Get-Content -Path $CodeFilePath -Raw
                }
            }
        }
        return $Server.Tasks.Create($id, $Options, $AutoStart.IsPresent, $Name, $Description)
    }
}
