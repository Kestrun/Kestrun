[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

Describe 'Get-KrAssignedVariable' {
    BeforeAll {
        . "$PSScriptRoot/../../../src/PowerShell/Kestrun/Private/Variable/Get-KrAssignedVariables.ps1"
    }

    It 'resolves non-constant values with -FromParent (caller scope)' {
        function __KrTestCaller {
            $srv = [pscustomobject]@{ Name = 'Demo'; Value = 123 }
            return Get-KrAssignedVariable -FromParent -ResolveValues -AsDictionary
        }

        $vars = __KrTestCaller
        $vars.ContainsKey('srv') | Should -BeTrue
        $vars['srv'].Name | Should -Be 'Demo'
        $vars['srv'].Value | Should -Be 123
    }

    It 'captures typed declaration-only variables (no assignment)' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            [int]$declOnly
            [int]$assigned = 42

            $museumHoursValue = @(
                [ordered]@{ date = '2023-09-11'; timeOpen = '09:00'; timeClose = '18:00' }
                [ordered]@{ date = '2023-09-12'; timeOpen = '09:00'; timeClose = '18:00' }
            )
        }

        @($vars | Where-Object Name -EQ 'declOnly').Count | Should -Be 1
        ($vars | Where-Object Name -EQ 'declOnly').Type | Should -Be ([int])
        ($vars | Where-Object Name -EQ 'declOnly').IsNullable | Should -BeFalse

        @($vars | Where-Object Name -EQ 'assigned').Count | Should -Be 1
        ($vars | Where-Object Name -EQ 'assigned').Value | Should -Be 42

        @($vars | Where-Object Name -EQ 'museumHoursValue').Count | Should -Be 1
        (($vars | Where-Object Name -EQ 'museumHoursValue').Value.Count) | Should -Be 2
    }

    It 'excludes variables listed in -ExcludeVariables' {
        $vars = Get-KrAssignedVariable -ResolveValues -ExcludeVariables @('skipMe', '$skipMe2') -ScriptBlock {
            $keepMe = 1
            $skipMe = 2
            [int]$skipMe2
        }

        ($vars.Name -contains 'skipMe') | Should -BeFalse
        ($vars.Name -contains 'skipMe2') | Should -BeFalse
        ($vars.Name -contains 'keepMe') | Should -BeTrue
    }

    It 'treats -ExcludeVariables as case-insensitive strict names' {
        $vars = Get-KrAssignedVariable -ResolveValues -ExcludeVariables @('SKIPME') -ScriptBlock {
            $skipMe = 123
            $keepMe = 456
        }

        ($vars.Name -contains 'skipMe') | Should -BeFalse
        ($vars.Name -contains 'keepMe') | Should -BeTrue
    }

    It 'excludes Set-Variable/New-Variable when -IncludeSetVariable is used' {
        $vars = Get-KrAssignedVariable -ResolveValues -IncludeSetVariable -ExcludeVariables @('hidden') -ScriptBlock {
            Set-Variable -Name 'hidden' -Value 1
            New-Variable -Name 'visible' -Value 2
        }

        ($vars.Name -contains 'hidden') | Should -BeFalse
        ($vars.Name -contains 'visible') | Should -BeTrue
    }

    It 'keeps last occurrence per (ScopeHint, Name)' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            $x = 1
            $x = 2
        }

        @($vars | Where-Object Name -EQ 'x').Count | Should -Be 1
        ($vars | Where-Object Name -EQ 'x').Value | Should -Be 2
    }

    It 'captures explicit scope prefixes on assignments' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            $local:foo = 1
            $script:bar = 2
        }

        ($vars | Where-Object Name -EQ 'foo').ScopeHint | Should -Be 'Local'
        ($vars | Where-Object Name -EQ 'foo').Value | Should -Be 1

        ($vars | Where-Object Name -EQ 'bar').ScopeHint | Should -Be 'Script'
        ($vars | Where-Object Name -EQ 'bar').Value | Should -Be 2
    }

    It 'captures non-Equals assignment operators (no initializer extraction)' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            $x += 1
            $y = 2
        }

        ($vars.Name -contains 'x') | Should -BeTrue
        ($vars | Where-Object Name -EQ 'x').Operator | Should -Be 'PlusEquals'
        ($vars | Where-Object Name -EQ 'x').Value | Should -Be $null

        ($vars.Name -contains 'y') | Should -BeTrue
    }

    It 'captures complex typed declaration-only variables' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            [Nullable[datetime]]$startDate
        }

        @($vars | Where-Object Name -EQ 'startDate').Count | Should -Be 1
        ($vars | Where-Object Name -EQ 'startDate').Type | Should -Be ([datetime])
        ($vars | Where-Object Name -EQ 'startDate').DeclaredType | Should -Be 'Nullable[datetime]'
        ($vars | Where-Object Name -EQ 'startDate').IsNullable | Should -BeTrue
        ($vars | Where-Object Name -EQ 'startDate').Value | Should -Be $null
    }

    It 'can return a Dictionary for C# interop (-AsDictionary)' {
        $dict = Get-KrAssignedVariable -ResolveValues -AsDictionary -ScriptBlock {
            $a = 1
            $b = 'two'
            $a = 3
        }

        $dict.GetType().FullName | Should -Match '^System\.Collections\.Generic\.Dictionary`2\[\[System\.String, (System\.Private\.CoreLib|mscorlib)'
        $dict['a'] | Should -Be 3
        $dict['b'] | Should -Be 'two'
    }

    It 'wraps typed assigned variables in -AsDictionary (metadata available even when value is non-null)' {
        $dict = Get-KrAssignedVariable -ResolveValues -AsDictionary -ScriptBlock {
            [int]$paginationLimit = 20
        }

        $dict.ContainsKey('paginationLimit') | Should -BeTrue
        $meta = $dict['paginationLimit']
        ($meta -is [System.Collections.IDictionary]) | Should -BeTrue

        $meta['__kestrunVariable'] | Should -BeTrue
        $meta['Value'] | Should -Be 20
        $meta['DeclaredType'] | Should -Be 'int'
        ($meta['Type'] -is [System.Type]) | Should -BeTrue
        $meta['Type'] | Should -Be ([int])
        $meta['IsNullable'] | Should -BeFalse
    }

    It 'preserves declared type metadata in -AsDictionary for typed declaration-only variables' {
        $dict = Get-KrAssignedVariable -ResolveValues -AsDictionary -ScriptBlock {
            [Nullable[datetime]]$startDate
        }

        $dict.ContainsKey('startDate') | Should -BeTrue
        $meta = $dict['startDate']
        ($meta -is [System.Collections.IDictionary]) | Should -BeTrue

        $meta['__kestrunVariable'] | Should -BeTrue
        $meta['Value'] | Should -Be $null
        $meta['DeclaredType'] | Should -Be 'Nullable[datetime]'
        ($meta['Type'] -is [System.Type]) | Should -BeTrue
        $meta['Type'] | Should -Be ([datetime])
        $meta['IsNullable'] | Should -BeTrue
    }

    It 'detects validation attributes on typed assignment variables' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            [int]$Port = 5000
            [ValidateNotNull()][int]$Foo = 1
        }

        ($vars | Where-Object Name -EQ 'Port').DeclaredType | Should -Be 'int'
        ($vars | Where-Object Name -EQ 'Port').HasAttributes | Should -BeFalse

        ($vars | Where-Object Name -EQ 'Foo').DeclaredType | Should -Be 'int'
        ($vars | Where-Object Name -EQ 'Foo').HasAttributes | Should -BeTrue
    }

    It 'detects validation attributes on typed declaration-only variables' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            [ValidateNotNull()][int]$Foo
            [int]$Bar
        }

        ($vars | Where-Object Name -EQ 'Foo').DeclaredType | Should -Be 'int'
        ($vars | Where-Object Name -EQ 'Foo').HasAttributes | Should -BeTrue

        ($vars | Where-Object Name -EQ 'Bar').DeclaredType | Should -Be 'int'
        ($vars | Where-Object Name -EQ 'Bar').HasAttributes | Should -BeFalse
    }

    It 'treats type constraints like [int] as not having attributes' {
        $vars = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
            [int]$Port = 5000
        }

        ($vars | Where-Object Name -EQ 'Port').HasAttributes | Should -BeFalse
    }

    It 'filters only attributed variables when -WithoutAttributesOnly is used (list output)' {
        $vars = Get-KrAssignedVariable -ResolveValues -WithoutAttributesOnly -ScriptBlock {
            [int]$Port = 5000
            [ValidateNotNull()][int]$Foo = 1
            $Baz = 3
        }

        ($vars.Name -contains 'Port') | Should -BeTrue
        ($vars.Name -contains 'Baz') | Should -BeTrue
        ($vars.Name -contains 'Foo') | Should -BeFalse
    }

    It 'filters only attributed variables when -WithoutAttributesOnly is used (-AsDictionary)' {
        $dict = Get-KrAssignedVariable -ResolveValues -AsDictionary -WithoutAttributesOnly -ScriptBlock {
            [int]$Port = 5000
            [ValidateNotNull()][int]$Foo = 1
            $Baz = 3
        }

        $dict.ContainsKey('Port') | Should -BeTrue
        $dict.ContainsKey('Baz') | Should -BeTrue
        $dict.ContainsKey('Foo') | Should -BeFalse

        # Typed, no-attribute variables still wrap metadata
        $meta = $dict['Port']
        ($meta -is [System.Collections.IDictionary]) | Should -BeTrue
        $meta['DeclaredType'] | Should -Be 'int'
        $meta['Value'] | Should -Be 5000
    }

    It 'treats multiple validation attributes as attributes and filters them' {
        $vars = Get-KrAssignedVariable -ResolveValues -WithoutAttributesOnly -ScriptBlock {
            [ValidateNotNull()][ValidatePattern('^x$')][string]$Foo = 'x'
            [string]$Bar = 'y'
        }

        ($vars.Name -contains 'Foo') | Should -BeFalse
        ($vars.Name -contains 'Bar') | Should -BeTrue
    }
}
