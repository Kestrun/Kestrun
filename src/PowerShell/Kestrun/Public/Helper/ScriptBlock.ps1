<#
    .SYNOPSIS
        Adds a named scriptblock to the specified scope, allowing retrieval via a getter function.
    .DESCRIPTION
        This function allows you to define a scriptblock with a name and an optional scope (Global or Script).
        The scriptblock can be retrieved later using a getter function that is automatically created.
    .PARAMETER Name
        The name of the scriptblock. If the name includes a scope prefix (e.g., `global:` or `script:`), it will be used as the scope unless overridden by the `-Scope` parameter.
    .PARAMETER Operator
        An optional operator that can be used to separate the name from the scriptblock. This is only applicable when using the `WithEquals` parameter set.
    .PARAMETER ScriptBlock
        The scriptblock to be associated with the specified name and scope.
    .PARAMETER Scope
        The scope in which to define the scriptblock. Valid values are `Global` or `Script`. If not specified, it defaults to `Script`.
    .EXAMPLE
        Add-KrScriptBlock -Name 'MyScript' -ScriptBlock { Write-Host "Hello, World!" }
        This creates a scriptblock named `MyScript` in the `Script` scope that writes "Hello, World!" to the console.
    .EXAMPLE
        Add-KrScriptBlock -Name 'global:MyGlobalScript' -ScriptBlock { Write-Host "Hello from Global!" } -Scope Global
        This creates a scriptblock named `MyGlobalScript` in the `Global` scope that writes "Hello from Global!" to the console.
    .NOTES
        This function is part of the Kestrun PowerShell module and is designed to facilitate the management of scriptblocks.
    .LINK
        https://github.com/Kestrun/Kestrun
#>
function Add-KrScriptBlock {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'Split')]
    param(
        # Style 1/2: separate name (+ optional '=') and scriptblock
        [Parameter(Mandatory, Position = 0, ParameterSetName = 'Split')]
        [Parameter(Mandatory, Position = 0, ParameterSetName = 'WithEquals')]
        [string]$Name,

        # Style 2 only: allow bare '=' token between name and scriptblock
        [Parameter(Mandatory, Position = 1, ParameterSetName = 'WithEquals')]
        [ValidateSet('=')]
        [string]$Operator,

        [Parameter(Mandatory, Position = 1, ParameterSetName = 'Split')]
        [Parameter(Mandatory, Position = 2, ParameterSetName = 'WithEquals')]
        [scriptblock]$ScriptBlock,

        # Optional explicit scope
        [Parameter(Position = 99)]
        [ValidateSet('Global', 'Script')]
        [string]$Scope
    )

    if ($PSCmdlet.ParameterSetName -eq 'Split') {

        # Parse "name = { ... }" packed into one argument
        $m2 = [regex]::Match(
            $Name,
            '^\s*(?:(global|script):)?\s*([A-Za-z_][\w\-.]*)\s*=\s*$'
        )
        if ($m2.Success) {
            if (-not $Scope) {
                $Scope = if ($m2.Groups[1].Value) { $m2.Groups[1].Value } else { 'Script' }
            }
            $Name = $m2.Groups[2].Value
            # If user also typed a separate '=' (Operator), ignore it
            if ($PSBoundParameters.ContainsKey('Operator')) {
                $null = $PSBoundParameters.Remove('Operator')
            }
        }
    }
    # If scope was embedded in the name (e.g., global:foo), honor it unless -Scope was passed
    $CleanName = $Name
    if (-not $PSBoundParameters.ContainsKey('Scope') -and $Name -match '^(global|script):(.+)$') {
        $Scope = $matches[1]
        $CleanName = $matches[2]
    }
    if (-not $Scope) { $Scope = 'Script' }

    Set-Item -Path function:$($Scope):Get-KrScriptBlock_$CleanName -Value "return {$($ScriptBlock.ToString())}"

    # Alias: ScriptBlock:<name> → getter
    Set-Alias -Name "ScriptBlock:$CleanName" -Value "Get-KrScriptBlock_$CleanName" -Scope $Scope
}

# Convenience alias
<#
.SYNOPSIS
    Creates a convenience alias for the Add-KrScriptBlock function.
.DESCRIPTION
    This alias allows users to call the Add-KrScriptBlock function simply by typing `ScriptBlock`.
.EXAMPLE
    ScriptBlock MyScript = { Write-Host "Hello, World!" }
    This is equivalent to calling Add-KrScriptBlock -Name 'MyScript' -ScriptBlock { Write-Host "Hello, World!" } with the same parameters.
#>
Set-Alias -Name ScriptBlock -Value Add-KrScriptBlock -Scope Global

