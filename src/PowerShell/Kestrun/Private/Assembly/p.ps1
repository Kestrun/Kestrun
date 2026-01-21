<#
.SYNOPSIS
    Resolves the scripting language from the file extension of the provided path.
.DESCRIPTION
    This helper function infers the scripting language (CSharp or VBNet) based on the
file extension of the given path. It throws an error if the extension is unsupported or missing.
.PARAMETER Path
    The file path from which to infer the scripting language.
.OUTPUTS
    [Kestrun.Scripting.ScriptLanguage]
    The inferred scripting language.
.EXAMPLE
    $lang = Resolve-KrCodeLanguageFromPath -Path 'script.cs'
    This command infers the scripting language as CSharp from the .cs extension.
.EXAMPLE
    $lang = Resolve-KrCodeLanguageFromPath -Path 'script.vb'
    This command infers the scripting language as VBNet from the .vb extension.
.EXAMPLE
    $lang = Resolve-KrCodeLanguageFromPath -Path 'script.ps1'
    This command infers the scripting language as PowerShell from the .ps1 extension.
.EXAMPLE
    $lang = Resolve-KrCodeLanguageFromPath -Path 'script.txt'
    This command throws an error because .txt is not a supported extension.
.EXAMPLE
    $lang = Resolve-KrCodeLanguageFromPath -Path 'script'
    This command throws an error because there is no file extension to infer the language.
.NOTES
    This function is used internally by Set-KrServerHttpsOptions to determine the language for client
    certificate validation code snippets.
#>
function Resolve-KrCodeLanguageFromPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $ext = [System.IO.Path]::GetExtension($Path)
    if ([string]::IsNullOrWhiteSpace($ext)) {
        throw "Unable to infer the language code type: '$Path' has no extension."
    }

    switch ($ext.ToLowerInvariant()) {
        '.cs' { return [Kestrun.Scripting.ScriptLanguage]::CSharp }
        '.vb' { return [Kestrun.Scripting.ScriptLanguage]::VBNet }
        '.ps1' { return [Kestrun.Scripting.ScriptLanguage]::PowerShell }
        default {
            throw "Unsupported language code file extension '$ext'."
        }
    }
}
