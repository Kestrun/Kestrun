<#
    .SYNOPSIS
        This function analyzes a PowerShell script block and identifies instances where the New-Object cmdlet is used.
    .DESCRIPTION
        The function scans the provided script block for occurrences of the New-Object cmdlet and returns a diagnostic record
        for each instance found. It is intended to encourage the use of the ::new() method instead of New-Object for object instantiation.
    .OUTPUTS
        A collection of diagnostic records, each indicating an instance of New-Object usage.
#>
function Measure-AvoidNewObjectRule {
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.Language.ScriptBlockAst]
        $ScriptBlockAst
    )

    # Initialize an empty array to collect diagnostic records
    $diagnostics = @()

    try {
        # Traverse the AST to find all instances of New-Object cmdlet
        $ScriptBlockAst.FindAll({
                param($Ast)
                $Ast -is [System.Management.Automation.Language.CommandAst] -and
                $Ast.CommandElements[0].Extent.Text -eq 'New-Object'
            }, $true) | ForEach-Object {
            $diagnostics += [PSCustomObject]@{
                Message = "Avoid using 'New-Object' and use '::new()' instead."
                Extent = $_.Extent
                RuleName = 'AvoidNewObjectRule'
                Severity = 'Warning'
                ScriptName = $FileName
            }
        }

        # Return the diagnostic records
        return $diagnostics
    } catch {
        $PSCmdlet.ThrowTerminatingError($PSItem)
    }
}

Export-ModuleMember -Function Measure-AvoidNewObjectRule
