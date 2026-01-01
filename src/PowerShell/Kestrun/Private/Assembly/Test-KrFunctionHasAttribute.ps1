<#
.SYNOPSIS
    Tests whether a given PowerShell function has a specified attribute applied.
.DESCRIPTION
    This function checks if the provided PowerShell function (CommandInfo) has an attribute that matches
    the specified attribute name regex. It first checks the runtime attributes applied to the function's
    ScriptBlock, and if not found, it parses the function's definition to look for attributes in the AST.
.PARAMETER Command
    The PowerShell function (CommandInfo) to check for the attribute.
.PARAMETER AttributeNameRegex
    A regex pattern to match the name of the attribute to look for.
.EXAMPLE
    PS> $cmd = Get-Command -Name 'MyFunction'
    PS> Test-KrFunctionHasAttribute -Command $cmd -AttributeNameRegex 'MyAttribute'
    Returns $true if 'MyFunction' has an attribute matching 'MyAttribute', otherwise $false.
.NOTES
    This function is part of the Kestrun PowerShell module.
#>
function Test-KrFunctionHasAttribute {
    [CmdletBinding()]
    [outputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.CommandInfo]$Command,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$AttributeNameRegex
    )
    try {
        $sb = $Command.ScriptBlock
        if (-not $sb) {
            return $false
        }

        # Prefer runtime attributes: this is what PowerShell actually binds
        foreach ($a in @($sb.Attributes)) {
            $t = $a.GetType()
            if ($t.Name -match $AttributeNameRegex -or $t.FullName -match $AttributeNameRegex) {
                return $true
            }
        }

        # Fallback: parse the definition and scan AttributeAst nodes
        $def = $Command.Definition
        if (-not $def) {
            return $false
        }

        $tokens = $null
        $parseErrors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseInput($def, [ref]$tokens, [ref]$parseErrors)

        if ($parseErrors -and $parseErrors.Count -gt 0) {
            return $false
        }

        $found = $ast.FindAll({
                param($n)
                $n -is [System.Management.Automation.Language.AttributeAst] -and
                ($n.TypeName?.Name -match $AttributeNameRegex)
            }, $true)

        return ($found.Count -gt 0)
    } catch {
        return $false
    }
    return $false
}
