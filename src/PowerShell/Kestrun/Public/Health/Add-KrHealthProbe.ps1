<#
    .SYNOPSIS
        Registers a script-based health probe with the active Kestrun server.
    .DESCRIPTION
        Wraps the Kestrun host AddProbe overload that accepts script content. You can provide inline
        PowerShell via the -ScriptBlock parameter set, inline code tied to an explicit language, or
        the path to a script file. Optional arguments, imports, references, and tag metadata can be
        supplied to influence probe execution and filtering.
    .PARAMETER Server
        The Kestrun host instance to configure. If omitted, the current server context is resolved automatically.
    .PARAMETER Name
        Unique name for the probe.
    .PARAMETER Tags
        Optional set of tags used to include or exclude the probe when requests filter by tag.
    .PARAMETER ScriptBlock
        Inline PowerShell that returns a ProbeResult (or equivalent contract). This is the default parameter set.
    .PARAMETER Code
        Inline code interpreted in the language supplied via -Language.
    .PARAMETER Language
        Script language for inline code or script files. When registering a script block the language defaults to PowerShell.
    .PARAMETER CodePath
        Path to a script file. The file is read once at registration time.
    .PARAMETER Arguments
        Hashtable of values exposed to the probe at execution time.
    .PARAMETER ExtraImports
        Additional language-specific imports (namespaces) supplied to Roslyn-based probes.
    .PARAMETER ExtraRefs
        Additional assemblies referenced by Roslyn-based probes.
    .PARAMETER PassThru
        Emits the configured server instance so the call can be chained.
    .EXAMPLE
        Add-KrHealthProbe -Name SelfCheck -Tags 'core' -ScriptBlock {
            return [Kestrun.Health.ProbeResult]::new([Kestrun.Health.ProbeStatus]::Healthy, 'Service ready')
        }
        Registers a PowerShell health probe named SelfCheck tagged with 'core'.
    .EXAMPLE
        Add-KrHealthProbe -Name Database -Language CSharp -Code @"
            return await ProbeAsync();
        "@
        Registers an inline C# health probe.
    .EXAMPLE
        Add-KrHealthProbe -Name Cache -CodePath './Scripts/CacheProbe.cs' -Language CSharp -ExtraImports 'System.Net'
        Registers a C# health probe from a script file and adds an extra namespace import.
#>
function Add-KrHealthProbe {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'ScriptBlock')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [string[]]$Tags,

        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = 'ScriptBlock')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [string]$Code,

        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'File')]
        [Kestrun.Scripting.ScriptLanguage]$Language,

        [Parameter(Mandatory = $true, ParameterSetName = 'File')]
        [string]$CodePath,

        [hashtable]$Arguments,

        [string[]]$ExtraImports,

        [System.Reflection.Assembly[]]$ExtraRefs,

        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Call New-KrServer first or pipe an existing host instance.'
        }
    }
    process {
        $codeToRegister = $null
        [Kestrun.Scripting.ScriptLanguage]$effectiveLanguage = [Kestrun.Scripting.ScriptLanguage]::PowerShell

        switch ($PSCmdlet.ParameterSetName) {
            'ScriptBlock' {
                $codeToRegister = $ScriptBlock.ToString()
                $effectiveLanguage = [Kestrun.Scripting.ScriptLanguage]::PowerShell
            }
            'Code' {
                $codeToRegister = $Code
                $effectiveLanguage = $Language
            }
            'File' {
                if (-not (Test-Path -LiteralPath $CodePath)) {
                    throw "The specified code path '$CodePath' does not exist. Ensure the file exists and the path is correct."
                }
                $codeToRegister = Get-Content -LiteralPath $CodePath -Raw
                if ($PSBoundParameters.ContainsKey('Language')) {
                    $effectiveLanguage = $Language
                } else {
                    $extension = [System.IO.Path]::GetExtension($CodePath).ToLowerInvariant()
                    $effectiveLanguage = switch ($extension) {
                        '.ps1' { [Kestrun.Scripting.ScriptLanguage]::PowerShell }
                        '.cs' { [Kestrun.Scripting.ScriptLanguage]::CSharp }
                        '.vb' { [Kestrun.Scripting.ScriptLanguage]::VBNet }
                        default {
                            throw "Cannot infer script language from extension '$extension'. Specify -Language explicitly."
                        }
                    }
                }
            }
            default {
                throw "Unsupported parameter set '$($PSCmdlet.ParameterSetName)'."
            }
        }

        if ([string]::IsNullOrWhiteSpace($codeToRegister)) {
            throw 'Probe code cannot be empty.'
        }

        $normalizedTags = $null
        if ($PSBoundParameters.ContainsKey('Tags')) {
            $normalizedTags = @($Tags | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
            if ($normalizedTags.Count -eq 0) {
                $normalizedTags = $null
            }
        }

        $argDictionary = $null
        if ($PSBoundParameters.ContainsKey('Arguments') -and $Arguments) {
            $argDictionary = [System.Collections.Generic.Dictionary[string, object]]::new()
            foreach ($key in $Arguments.Keys) {
                $argDictionary[$key] = $Arguments[$key]
            }
        }

        $imports = $null
        if ($PSBoundParameters.ContainsKey('ExtraImports')) {
            $imports = @($ExtraImports | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
            if ($imports.Count -eq 0) {
                $imports = $null
            }
        }

        try {
            $hostResult = $Server.AddProbe($Name, $normalizedTags, $codeToRegister, $effectiveLanguage, $argDictionary, $imports, $ExtraRefs)
            Write-KrLog -Level Information -Message "Health probe '{0}' registered." -Value $Name
            if ($PassThru.IsPresent) {
                return $hostResult
            }
        } catch {
            Write-KrLog -Level Error -Message "Failed to register health probe '{0}'." -Value $Name -ErrorRecord $_
            throw
        }
    }
}
