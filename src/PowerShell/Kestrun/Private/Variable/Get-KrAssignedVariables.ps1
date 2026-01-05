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
    .PARAMETER ExcludeVariables
      Array of variable names to exclude from the results.
    .OUTPUTS
      PSCustomObject with properties: Name, ScopeHint, ProviderPath, Source, Operator,
        Type, Value.
    .EXAMPLE
      Get-KrAssignedVariable -FromParent -ResolveValues
      Scans the currently executing scriptblock for assigned variables and resolves their values.
    .NOTES
      This function is used by Enable-KrConfiguration to capture user-defined variables
      from the caller script and inject them into the Kestrun server's shared state.
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

        [Parameter()]
        [switch]$IncludeSetVariable,

        [Parameter()]
        [switch]$ResolveValues,

        [Parameter()]
        [switch]$AsDictionary,

        [Parameter()]
        [ValidateSet('Local', 'Script', 'Global')]
        [string]$DefaultScope = 'Script',

        [Parameter()]
        [string[]]$ExcludeVariables
    )

    $excludeSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $excludeList = if ($null -ne $ExcludeVariables) { $ExcludeVariables } else { @() }
    foreach ($ev in $excludeList) {
        if ([string]::IsNullOrWhiteSpace($ev)) { continue }
        $n = $ev.Trim()
        if ($n.StartsWith('$')) { $n = $n.Substring(1) }
        if (-not [string]::IsNullOrWhiteSpace($n)) {
            [void]$excludeSet.Add($n)
        }
    }

    $rows = [System.Collections.Generic.List[object]]::new()

    <#
    .SYNOPSIS
         Extracts the variable name and scope from a VariablePath.
    .DESCRIPTION
         This function analyzes a VariablePath object to determine the variable's name and scope.
    .PARAMETER variablePath
         The VariablePath object to analyze.
    .OUTPUTS
         PSCustomObject with properties: Name, ScopeHint, ProviderPath.
    #>
    function _GetScopeAndNameFromVariablePath([System.Management.Automation.VariablePath] $variablePath) {
        $raw = $variablePath.UserPath
        if (-not $raw) {
            $raw = $variablePath.UnqualifiedPath
        }
        if (-not $raw) {
            return $null
        }

        $name = $raw
        $scopeHint = $DefaultScope

        if ($name -match '^(?<scope>global|script|local|private):(?<rest>.+)$') {
            $scopeHint = ($Matches.scope.Substring(0, 1).ToUpperInvariant() + $Matches.scope.Substring(1).ToLowerInvariant())
            $name = $Matches.rest
        }

        # ignore member/index forms and ${foo}
        if ($name.Contains('.') -or $name.Contains('[')) {
            return $null
        }
        if ($name.StartsWith('{') -and $name.EndsWith('}')) {
            $name = $name.Substring(1, $name.Length - 2)
        }

        if (-not $name) {
            return $null
        }

        [pscustomobject]@{
            Name = $name
            ScopeHint = $scopeHint
            ProviderPath = "variable:$($scopeHint):$name"
        }
    }

    <#
    .SYNOPSIS
        Attempts to resolve the value of a variable by name and scope.
    .DESCRIPTION
        This function tries to get the value of a variable from the specified scope.
        If -ResolveValues is not specified, it returns $null.
    .PARAMETER name
        The name of the variable to resolve.
    .PARAMETER scopeHint
        The scope hint (Local, Script, Global) to use for resolution.
    .OUTPUTS
        The value of the variable if found; otherwise, $null.
    #>
    function _TryResolveValue([string] $name, [string] $scopeHint) {
        if (-not $ResolveValues.IsPresent) {
            return $null
        }

        # When using -FromParent, prefer numeric scope resolution so we can see the caller's values.
        if ($FromParent.IsPresent) {
            try {
                return Get-Variable -Name $name -Scope $scopeUp -ValueOnly -ErrorAction SilentlyContinue
            } catch {
                return $null
            }
        }

        try {
            return (Get-Item -ErrorAction SilentlyContinue "variable:${scopeHint}:$name").Value
        } catch {
            return $null
        }
    }

    <#
    .SYNOPSIS
        Attempts to extract the initializer value from an assignment AST node.
    .DESCRIPTION
        This function analyzes an AssignmentStatementAst to extract the value being assigned,
        if possible. It supports simple constant expressions, arrays, hashtables, and some
        common expression types.
    .PARAMETER assignmentAst
        The AssignmentStatementAst node to analyze.
    .OUTPUTS
        The extracted value if resolvable; otherwise, $null.
    #>
    function _TryGetInitializerValueFromAssignment([System.Management.Automation.Language.AssignmentStatementAst] $assignmentAst) {
        if (-not $ResolveValues.IsPresent) {
            return $null
        }
        if ($assignmentAst.Operator -ne [System.Management.Automation.Language.TokenKind]::Equals) {
            return $null
        }

        $expr = $null
        if ($assignmentAst.Right -is [System.Management.Automation.Language.CommandExpressionAst]) {
            $expr = $assignmentAst.Right.Expression
        } elseif ($assignmentAst.Right -is [System.Management.Automation.Language.PipelineAst]) {
            $p = $assignmentAst.Right
            if ($p.PipelineElements.Count -eq 1 -and $p.PipelineElements[0] -is [System.Management.Automation.Language.CommandExpressionAst]) {
                $expr = $p.PipelineElements[0].Expression
            }
        }

        function _EvalStatement([System.Management.Automation.Language.StatementAst] $s) {
            if ($null -eq $s) { return $null }

            if ($s -is [System.Management.Automation.Language.CommandExpressionAst]) {
                return _EvalExpr $s.Expression
            }

            if ($s -is [System.Management.Automation.Language.PipelineAst]) {
                if ($s.PipelineElements.Count -eq 1 -and $s.PipelineElements[0] -is [System.Management.Automation.Language.CommandExpressionAst]) {
                    return _EvalExpr $s.PipelineElements[0].Expression
                }
            }

            return $null
        }

        function _EvalKeyValuePart($node) {
            if ($null -eq $node) { return $null }

            if ($node -is [System.Management.Automation.Language.ExpressionAst]) {
                return _EvalExpr $node
            }

            if ($node -is [System.Management.Automation.Language.StatementAst]) {
                return _EvalStatement $node
            }

            return $null
        }

        function _EvalExpr([System.Management.Automation.Language.ExpressionAst] $e) {
            if ($null -eq $e) { return $null }

            switch ($e.GetType().FullName) {
                'System.Management.Automation.Language.ConstantExpressionAst' { return $e.Value }
                'System.Management.Automation.Language.StringConstantExpressionAst' { return $e.Value }
                'System.Management.Automation.Language.ExpandableStringExpressionAst' { return $e.Value }
                'System.Management.Automation.Language.TypeExpressionAst' { return $e.TypeName.FullName }
                'System.Management.Automation.Language.ParenthesisExpressionAst' {
                    if ($e.Pipeline -and $e.Pipeline.PipelineElements.Count -eq 1 -and $e.Pipeline.PipelineElements[0] -is [System.Management.Automation.Language.CommandExpressionAst]) {
                        return _EvalExpr $e.Pipeline.PipelineElements[0].Expression
                    }
                    return $null
                }
                'System.Management.Automation.Language.ConvertExpressionAst' {
                    # e.g. [int]42 or [ordered]@{...}
                    if ($e.Child -is [System.Management.Automation.Language.ExpressionAst]) {
                        $childValue = _EvalExpr $e.Child
                        if ($e.Type -and $e.Type.TypeName -and ($e.Type.TypeName.FullName -ieq 'ordered') -and ($e.Child -is [System.Management.Automation.Language.HashtableAst])) {
                            # Rebuild as an ordered dictionary
                            $ordered = [ordered]@{}
                            foreach ($kv in $e.Child.KeyValuePairs) {
                                $k = _EvalKeyValuePart $kv.Item1
                                $v = _EvalKeyValuePart $kv.Item2
                                if ($null -eq $k) { return $null }
                                $ordered[$k] = $v
                            }
                            return $ordered
                        }

                        return $childValue
                    }
                    return $null
                }
                'System.Management.Automation.Language.ArrayExpressionAst' {
                    # @( ... )
                    if (-not $e.SubExpression) { return @() }
                    $items = @()
                    foreach ($st in $e.SubExpression.Statements) {
                        $vv = _EvalStatement $st
                        if ($null -eq $vv) { return $null }
                        $items += $vv
                    }
                    return , $items
                }
                'System.Management.Automation.Language.SubExpressionAst' {
                    # $( ... )
                    $items = @()
                    foreach ($st in $e.Statements) {
                        $vv = _EvalStatement $st
                        if ($null -eq $vv) { return $null }
                        $items += $vv
                    }
                    return , $items
                }
                'System.Management.Automation.Language.UnaryExpressionAst' {
                    $v = $null
                    if ($e.Child -is [System.Management.Automation.Language.ExpressionAst]) {
                        $v = _EvalExpr $e.Child
                    }
                    if ($e.TokenKind -eq [System.Management.Automation.Language.TokenKind]::Minus -and $v -is [ValueType]) {
                        try { return -1 * $v } catch { return $null }
                    }
                    return $null
                }
                'System.Management.Automation.Language.ArrayLiteralAst' {
                    $arr = @()
                    foreach ($el in $e.Elements) {
                        if ($el -isnot [System.Management.Automation.Language.ExpressionAst]) { return $null }
                        $vv = _EvalExpr $el
                        if ($null -eq $vv -and ($el -isnot [System.Management.Automation.Language.ConstantExpressionAst])) { return $null }
                        $arr += $vv
                    }
                    return , $arr
                }
                'System.Management.Automation.Language.HashtableAst' {
                    $ht = @{}
                    foreach ($kv in $e.KeyValuePairs) {
                        $k = _EvalKeyValuePart $kv.Item1
                        $v = _EvalKeyValuePart $kv.Item2
                        if ($null -eq $k) { return $null }
                        $ht[$k] = $v
                    }
                    return $ht
                }
                default { return $null }
            }
        }

        if ($expr -is [System.Management.Automation.Language.ExpressionAst]) {
            return _EvalExpr $expr
        }

        return $null
    }

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
        if ($scopeUp -lt 1) { throw 'Parent frame not found.' }

        # prefer its live ScriptBlock; if null, rebuild from file
        $ScriptBlock = $frame.InvocationInfo.MyCommand.ScriptBlock
        if (-not $ScriptBlock -and $frame.ScriptName) {
            $ScriptBlock = [scriptblock]::Create((Get-Content -Raw -LiteralPath $frame.ScriptName))
        }
        if (-not $ScriptBlock) { throw 'Parent frame has no scriptblock or script file to parse.' }
    }

    if (-not $ScriptBlock) {
        throw 'No scriptblock provided. Use -FromParent or pass a ScriptBlock.'
    }
    # Use the original script text so offsets match exactly
    $scriptText = $ScriptBlock.Ast.Extent.Text

    # Find the first *actual command* invocation named Enable-KrConfiguration
    $enableCmd = $ScriptBlock.Ast.FindAll({
            param($node)

            if ($node -isnot [System.Management.Automation.Language.CommandAst]) { return $false }

            $name = $node.GetCommandName()
            return $name -and ($name -ieq 'Enable-KrConfiguration')
        }, $true) | Select-Object -First 1

    if ($enableCmd) {
        # Cut everything before that command
        $pre = $scriptText.Substring(0, $enableCmd.Extent.StartOffset).TrimEnd()

        # Preserve your brace-closing hack
        if ($pre.TrimStart().StartsWith('{')) {
            $pre += "`n}"
        }

        $ScriptBlock = [scriptblock]::Create($pre)
    }



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
            if ($p -is [System.Management.Automation.Language.ScriptBlockAst]) { break }
            $p = $p.Parent
        }
        return $false
    }

    function _IsInAssignment([System.Management.Automation.Language.Ast] $node) {
        # IMPORTANT: AST parent chains can escape the scanned ScriptBlockAst
        # (e.g. when the scriptblock literal is part of an outer assignment).
        # We only care about assignments that occur *within* the scanned scriptblock.
        $p = $node.Parent
        while ($p) {
            if ($p -is [System.Management.Automation.Language.AssignmentStatementAst]) { return $true }
            if ($p -is [System.Management.Automation.Language.ScriptBlockAst]) { break }
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

        $info = _GetScopeAndNameFromVariablePath $varAst.VariablePath
        if (-not $info) { continue }
        if ($excludeSet.Contains($info.Name)) { continue }

        $val = _TryResolveValue -name $info.Name -scopeHint $info.ScopeHint
        if ($null -eq $val) {
            # If runtime resolution fails (common when scanning caller scripts from a module),
            # fall back to reading simple constant initializers directly from the AST.
            $val = _TryGetInitializerValueFromAssignment -assignmentAst $a
        }
        $type = if ($null -ne $val) { $val.GetType().FullName } else { $null }

        [void]$rows.Add([pscustomobject]@{
                Name = $info.Name
                ScopeHint = $info.ScopeHint
                ProviderPath = $info.ProviderPath
                Source = 'Assignment'
                Operator = $a.Operator.ToString()
                Type = $type
                Value = $val
            })
    }

    # Also capture declaration-only typed variables like: [int]$x  (no assignment)
    # We scan ConvertExpressionAst directly to reliably catch both plain and attributed declarations.
    $convertAsts = $ScriptBlock.Ast.FindAll(
        {
            param($n)
            $n -is [System.Management.Automation.Language.ConvertExpressionAst] -and
            $n.Child -is [System.Management.Automation.Language.VariableExpressionAst]
        },
        $true
    )

    foreach ($c in $convertAsts) {
        # Skip casts that are part of assignments (those are already handled as assignments)
        if (_IsInAssignment $c) {
            continue
        }

        $varExpr = [System.Management.Automation.Language.VariableExpressionAst]$c.Child
        $info = _GetScopeAndNameFromVariablePath $varExpr.VariablePath
        if (-not $info) { continue }
        if ($excludeSet.Contains($info.Name)) { continue }

        $declaredType = $c.Type.TypeName.FullName
        $val = _TryResolveValue -name $info.Name -scopeHint $info.ScopeHint

        [void]$rows.Add([pscustomobject]@{
                Name = $info.Name
                ScopeHint = $info.ScopeHint
                ProviderPath = $info.ProviderPath
                Source = 'Declaration'
                Operator = $null
                Type = $declaredType
                Value = $val
            })
    }

    if ($IncludeSetVariable) {
        $cmdAsts = $ScriptBlock.Ast.FindAll(
            { param($n) $n -is [System.Management.Automation.Language.CommandAst] -and -not (_IsInFunction $n) }, $true)

        foreach ($c in $cmdAsts) {
            $cmd = $c.GetCommandName()
            if ($cmd -notin 'Set-Variable', 'New-Variable') { continue }
            $named = @{}

            for ($i = 0; $i -lt $c.CommandElements.Count; $i++) {
                $e = $c.CommandElements[$i]
                if ($e -isnot [System.Management.Automation.Language.CommandParameterAst]) { continue }
                if ($e.ParameterName -notin 'Name', 'Scope') { continue }

                $argAst = $e.Argument
                if (-not $argAst -and ($i + 1) -lt $c.CommandElements.Count) {
                    $next = $c.CommandElements[$i + 1]
                    if ($next -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                        $argAst = $next
                        $i++
                    }
                }

                if ($argAst -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                    $named[$e.ParameterName] = $argAst.Value
                }
            }
            if ($named.ContainsKey('Name')) {
                $name = $named['Name']
                if ($excludeSet.Contains($name)) { continue }
                $scope = if ($named.ContainsKey('Scope') -and -not [string]::IsNullOrWhiteSpace($named['Scope'])) { $named['Scope'] } else { $DefaultScope }
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
    $final = @($rows | Group-Object ScopeHint, Name | ForEach-Object { $_.Group[-1] })

    if ($AsDictionary.IsPresent) {
        $dict = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($v in $final) {
            $dict[$v.Name] = $v.Value
        }
        return $dict
    }

    return $final
}
