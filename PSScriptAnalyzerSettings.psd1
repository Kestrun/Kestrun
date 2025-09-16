@{
    # Be explicit
    IncludeDefaultRules = $true

    # Drop PSAvoidUsingWriteHost at the top and keep the per-rule disable below, or uncomment here:
    # ExcludeRules = @()
    ExcludeRules = @(
        'PSAvoidUsingWriteHost'
        # PowerShell 7+ only: prefer UTF-8 without BOM for cross-platform tooling friendliness
        'PSUseBOMForUnicodeEncodedFile'
    )

    # Where your custom rules live (folder or file). Uncomment if you have custom rules.
    # CustomRulePath = @('./Lint')

    Rules = @{
        # Built-in rule tuning
        PSReviewUnusedParameter = @{
            Enable = $true
            CommandsToTraverse = @(
                'Where-Object', 'ForEach-Object', 'Group-Object', 'Sort-Object'
            )
        }

        # Style alignment (exists in PSSA)
        PSAlignAssignmentStatement = @{
            Enable = $true
            CheckHashtable = $false
            CheckPipeline = $true
        }

        PSAvoidLongLines = @{
            Enable = $true
            MaximumLineLength = 220
        }

        PSAvoidUsingCmdletAliases = @{
            # Allow only these aliases; use real aliases
            AllowList = @('foreach', 'where')  # tweak to taste
        }

        # Braces — comment and setting agree: open brace on the same line
        PSPlaceOpenBrace = @{
            Enable = $true
            OnSameLine = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
        }
        PSPlaceCloseBrace = @{
            Enable = $true
            NoEmptyLineBefore = $true
            IgnoreOneLineBlock = $true
            NewLineAfter = $false
        }

        PSProvideCommentHelp = @{
            Enable = $true
            ExportedOnly = $false
            BlockComment = $true
            VSCodeSnippetCorrection = $false
            Placement = 'before'
        }

        PSUseConsistentIndentation = @{
            Enable = $true
            IndentationSize = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            Kind = 'space'
        }

        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckInnerBrace = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $true
            CheckPipe = $true
            CheckPipeForRedundantWhitespace = $true
            CheckSeparator = $true
            CheckParameter = $true
            IgnoreAssignmentOperatorInsideHashTable = $true
        }

        PSUseCorrectCasing = @{
            Enable = $true
            CheckCommands = $true
            CheckKeyword = $true
            CheckOperator = $true
        }

        PSUseSingularNouns = @{
            Enable = $true
            NounAllowList = @('Data', 'Windows', 'Metadata', 'Settings', 'Options', 'Parameters', 'Credentials', 'Properties')
        }



        # Opinionated toggles
        PSAvoidUsingWriteHost = @{ Enable = $false }
        PSUseApprovedVerbs = @{ Enable = $true }
    }
}
