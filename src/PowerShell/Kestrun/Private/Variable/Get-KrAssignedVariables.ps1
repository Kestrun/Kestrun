<#
    .SYNOPSIS
      Find variables that are *defined/assigned* in a scriptblock and (optionally) fetch their values.
    .DESCRIPTION
      Scans the AST for AssignmentStatementAst where the LHS is a VariableExpressionAst,
      and optionally Set-Variable/New-Variable calls. Can resolve current values from
      the appropriate scope (local/script/global) in the *current* runspace.
    .PARAMETER ScriptBlock
      ScriptBlock to scan. If omitted, use -Current to inspect the currently running block.
    .PARAMETER FromParent
      Inspect the *currently executing* scriptblock (via $MyInvocation.MyCommand.ScriptBlock).
    .PARAMETER IncludeSetVariable
      Also detect variables created via Set-Variable/New-Variable (-Name … [-Scope …]).
    .PARAMETER ResolveValues
      Resolve current values from variable:<scope>:<name> and include Type/Value.
    .PARAMETER DefaultScope
      Scope to assume when the LHS had no explicit scope (default: Local).
  #>
function Get-KrAssignedVariable {
    [CmdletBinding(DefaultParameterSetName = 'Given')]
    param(
        [Parameter(ParameterSetName = 'Given', Position = 0)]
        [scriptblock]$ScriptBlock,

        # NEW: use the caller's scriptblock (parent frame)
        [Parameter(ParameterSetName = 'FromParent', Mandatory)]
        [switch]$FromParent,

        # How many frames up to climb (1 = immediate caller)
        [Parameter(ParameterSetName = 'FromParent')]
        [int]$Up = 1,

        # Optional: skip frames from these modules when searching
        [Parameter(ParameterSetName = 'FromParent')]
        [string[]]$ExcludeModules = @('Kestrun'),

        [switch]$IncludeSetVariable,
        [switch]$ResolveValues,
        [ValidateSet('Local', 'Script', 'Global')]
        [string]$DefaultScope = 'Script'
    )

    # ---------- resolve $ScriptBlock source ----------
    if ($FromParent.IsPresent) {
        $allFrames = Get-PSCallStack
        # 0 = this function, 1 = immediate caller, 2+ = higher parents
        $frames = $allFrames | Select-Object -Skip 1

        if ($ExcludeModules.Count) {
            $frames = $frames | Where-Object {
                $mn = $_.InvocationInfo.MyCommand.ModuleName
                -not ($mn -and ($mn -in $ExcludeModules))
            }
        }

        # pick the desired parent frame
        $frame = $frames | Select-Object -Skip ($Up - 1) -First 1
        if (-not $frame) { throw "No parent frame found (Up=$Up)." }

        # Figure out how far “up” that is compared to the original call stack
        $scopeUp = ($allFrames.IndexOf($frame)) + 1
        if ($scopeUp -lt 1) { throw "Parent frame not found." }

        # prefer its live ScriptBlock; if null, rebuild from file
        $ScriptBlock = $frame.InvocationInfo.MyCommand.ScriptBlock
        if (-not $ScriptBlock -and $frame.ScriptName) {
            $ScriptBlock = [scriptblock]::Create((Get-Content -Raw -LiteralPath $frame.ScriptName))
        }
        if (-not $ScriptBlock) { throw "Parent frame has no scriptblock or script file to parse." }
    }

    if (-not $ScriptBlock) {
        throw "No scriptblock provided. Use -FromParent or pass a ScriptBlock."
    }
    $ast = ($ScriptBlock.Ast).ToString()

    $endstring = $ast.IndexOf("Enable-KrConfiguration", [StringComparison]::OrdinalIgnoreCase)
    if ($endstring -lt 0) {
        throw "The provided scriptblock does not appear to contain 'Enable-KrConfiguration' call."
    }
    $ast = $ast.Substring(0, $endstring).Trim()
    if ($ast.StartsWith('{')) {
        $ast += "`n}"
    }
    $ScriptBlock = [scriptblock]::Create($ast)


    <#
   .SYNOPSIS
       Checks if a given AST node is inside a function.
   .DESCRIPTION
       This function traverses the parent nodes of the given AST node to determine if it is
       located within a function definition.
    .PARAMETER node
       The AST node to check.
    .OUTPUTS
       [bool] Returns true if the node is inside a function, false otherwise.
   #>
    function _IsInFunction([System.Management.Automation.Language.Ast] $node) {
        $p = $node.Parent
        while ($p) {
            if ($p -is [System.Management.Automation.Language.FunctionDefinitionAst]) { return $true }
            $p = $p.Parent
        }
        return $false
    }

    $assignAsts = $ScriptBlock.Ast.FindAll(
        { param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst] }, $true)

    foreach ($a in $assignAsts) {
        $varAst = $a.Left.Find(
            { param($n) $n -is [System.Management.Automation.Language.VariableExpressionAst] }, $true
        ) | Select-Object -First 1
        if (-not $varAst) { continue }

        $vp = $varAst.VariablePath
        $name = $vp.UnqualifiedPath

        if (-not $name) { $name = $vp.UserPath }            # ← fallback
        if (-not $name) { continue }
        $name = $name -replace '^[^:]*:', ''
        if ($name.Contains('.') -or $name.Contains('[')) { continue }
        if ($name.StartsWith('{') -and $name.EndsWith('}')) {
            $name = $name.Substring(1, $name.Length - 2)        # ← ${foo} → foo
        }
        $val = Get-Variable -Name $name -Scope $scopeUp -ErrorAction SilentlyContinue

        [pscustomobject]@{
            Name = $name
            ScopeHint = $scope
            ProviderPath = $provider
            Source = 'Assignment'
            Operator = $a.Operator.ToString()
            Type = $type
            Value = $val
        }
    }

    if ($IncludeSetVariable) {
        $cmdAsts = $ScriptBlock.Ast.FindAll(
            { param($n) $n -is [System.Management.Automation.Language.CommandAst] -and -not (_IsInFunction $n) }, $true)

        foreach ($c in $cmdAsts) {
            $cmd = $c.GetCommandName()
            if ($cmd -notin 'Set-Variable', 'New-Variable') { continue }
            $named = @{}
            foreach ($e in $c.CommandElements) {
                if ($e -is [System.Management.Automation.Language.CommandParameterAst] -and $e.ParameterName -in 'Name', 'Scope') {
                    $arg = $e.Argument
                    if ($arg -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                        $named[$e.ParameterName] = $arg.Value
                    }
                }
            }
            if ($named.ContainsKey('Name')) {
                $name = $named['Name']
                $scope = $named['Scope'] ?? $DefaultScope
                $provider = "variable:$($scope):$name"
                $val = $null; $type = $null
                if ($ResolveValues) {
                    try {
                        $val = (Get-Item -EA SilentlyContinue $provider).Value
                        if ($null -ne $val) { $type = $val.GetType().FullName }
                    } catch {
                        Write-Warning "Failed to resolve variable '$name' in scope '$scope': $_"
                    }
                }
                [pscustomobject]@{
                    Name = $name
                    ScopeHint = $scope
                    ProviderPath = $provider
                    Source = $cmd
                    Operator = $null
                    Type = $type
                    Value = $val
                } | ForEach-Object { [void]$rows.Add($_) }
            }
        }
    }

    # keep last occurrence per (ScopeHint, Name)
    $rows | Group-Object ScopeHint, Name | ForEach-Object { $_.Group[-1] }
}

