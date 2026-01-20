[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

Describe 'Get-KrAssignedVariable' {
    BeforeAll {
        . "$PSScriptRoot/../../../src/PowerShell/Kestrun/Private/Variable/Get-KrAssignedVariables.ps1"
    }

    It 'resolves non-constant values with -FromParent (caller scope)' {
        function __KrTestCaller {
            $srv = [pscustomobject]@{ Name = 'Demo'; Value = 123 }
            return Get-KrAssignedVariable -FromParent -ResolveValues -OutputStructure 'Dictionary'
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

    It 'can return a Dictionary for C# interop (-OutputStructure ''Dictionary'')' {
        $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Dictionary' -ScriptBlock {
            $a = 1
            $b = 'two'
            $a = 3
        }

        $dict.GetType().FullName | Should -Match '^System\.Collections\.Generic\.Dictionary`2\[\[System\.String, (System\.Private\.CoreLib|mscorlib)'
        $dict['a'] | Should -Be 3
        $dict['b'] | Should -Be 'two'
    }

    It 'wraps typed assigned variables in -OutputStructure ''Dictionary'' (metadata available even when value is non-null)' {
        $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Dictionary' -ScriptBlock {
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

    It 'preserves declared type metadata in -OutputStructure ''Dictionary'' for typed declaration-only variables' {
        $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Dictionary' -ScriptBlock {
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

    It 'filters only attributed variables when -WithoutAttributesOnly is used (-OutputStructure ''Dictionary'')' {
        $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Dictionary' -WithoutAttributesOnly -ScriptBlock {
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

    Context 'OutputStructure StringObjectMap' {
        It 'returns a simple Dictionary with just values (no metadata wrapping)' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ScriptBlock {
                [int]$Port = 5000
                $Name = 'MyServer'
                [Nullable[datetime]]$StartDate
            }

            $dict.GetType().FullName | Should -Match '^System\.Collections\.Generic\.Dictionary`2\[\[System\.String, (System\.Private\.CoreLib|mscorlib)'
            $dict.Count | Should -Be 3

            # Should be raw values, not wrapped metadata
            $dict['Port'] | Should -Be 5000
            $dict['Name'] | Should -Be 'MyServer'
            $dict['StartDate'] | Should -Be $null

            # Should NOT have metadata structure
            ($dict['Port'] -is [System.Collections.IDictionary]) | Should -BeFalse
            ($dict['Port'] -is [int]) | Should -BeTrue
        }

        It 'handles typed variables without wrapping metadata' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ScriptBlock {
                [int]$paginationLimit = 20
                [ValidateNotNull()][string]$Foo = 'bar'
            }

            $dict['paginationLimit'] | Should -Be 20
            $dict['Foo'] | Should -Be 'bar'

            # No metadata wrapping
            ($dict['paginationLimit'] -is [int]) | Should -BeTrue
            ($dict['Foo'] -is [string]) | Should -BeTrue
        }

        It 'handles declaration-only typed variables (value is null)' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ScriptBlock {
                [Nullable[datetime]]$startDate
                [int]$count
            }

            $dict.ContainsKey('startDate') | Should -BeTrue
            $dict.ContainsKey('count') | Should -BeTrue
            $dict['startDate'] | Should -Be $null
            # Declaration-only variables resolve to $null, not default type values
            $dict['count'] | Should -Be $null
        }

        It 'handles untyped variables' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ScriptBlock {
                $x = 1
                $y = 'two'
                $z = @(1, 2, 3)
            }

            $dict['x'] | Should -Be 1
            $dict['y'] | Should -Be 'two'
            $dict['z'].Count | Should -Be 3
        }

        It 'respects -ExcludeVariables' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ExcludeVariables @('skip') -ScriptBlock {
                $keep = 1
                $skip = 2
            }

            $dict.ContainsKey('keep') | Should -BeTrue
            $dict.ContainsKey('skip') | Should -BeFalse
        }

        It 'respects -WithoutAttributesOnly' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -WithoutAttributesOnly -ScriptBlock {
                [int]$Port = 5000
                [ValidateNotNull()][string]$Foo = 'bar'
            }

            $dict.ContainsKey('Port') | Should -BeTrue
            $dict.ContainsKey('Foo') | Should -BeFalse
            $dict['Port'] | Should -Be 5000
        }

        It 'handles complex values like arrays and hashtables' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ScriptBlock {
                $arr = @(1, 2, 3)
                $hash = @{ key = 'value' }
            }

            $dict['arr'].Count | Should -Be 3
            $dict['hash']['key'] | Should -Be 'value'
        }

        It 'keeps last occurrence when variable is assigned multiple times' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ScriptBlock {
                $x = 1
                $x = 2
                $x = 3
            }

            $dict['x'] | Should -Be 3
        }
    }

    Context 'OutputStructure coverage - all three formats' {
        It 'Array format returns full metadata objects' {
            $result = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Array' -ScriptBlock {
                [int]$Port = 5000
            }

            $result | Should -BeOfType [PSCustomObject]
            $result.Name | Should -Be 'Port'
            $result.Value | Should -Be 5000
            $result.DeclaredType | Should -Be 'int'
            $result.Type | Should -Be ([int])
        }

        It 'Dictionary format wraps typed variables, raw values for untyped' {
            $result = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Dictionary' -ScriptBlock {
                [int]$Typed = 1
                $Untyped = 2
            }

            # Typed variable wrapped
            ($result['Typed'] -is [System.Collections.IDictionary]) | Should -BeTrue
            $result['Typed']['__kestrunVariable'] | Should -BeTrue
            $result['Typed']['Value'] | Should -Be 1

            # Untyped variable direct value
            $result['Untyped'] | Should -Be 2
        }

        It 'StringObjectMap format returns raw values only (no wrapping)' {
            $result = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -ScriptBlock {
                [int]$Typed = 1
                $Untyped = 2
            }

            # Both return raw values
            $result['Typed'] | Should -Be 1
            $result['Untyped'] | Should -Be 2

            # No wrapping
            ($result['Typed'] -is [System.Collections.IDictionary]) | Should -BeFalse
        }
    }

    Context 'OutputStructure - additional permutations' {
        It 'defaults to Array when -OutputStructure is omitted' {
            $result = Get-KrAssignedVariable -ResolveValues -ScriptBlock {
                [int]$Port = 5000
            }

            # Should emit metadata objects (array of PSCustomObject)
            @($result).Count | Should -BeGreaterThan 0
            (@($result)[0] -is [pscustomobject]) | Should -BeTrue
            (@($result)[0]).Name | Should -Be 'Port'
        }

        It 'throws for invalid -OutputStructure value' {
            { Get-KrAssignedVariable -ResolveValues -OutputStructure 'InvalidChoice' -ScriptBlock { $x = 1 } } | Should -Throw
        }

        It 'supports -FromParent with OutputStructure Array' {
            function __KrTestCallerArray {
                $srv = [pscustomobject]@{ Name = 'Demo'; Value = 123 }
                return Get-KrAssignedVariable -FromParent -ResolveValues -OutputStructure 'Array'
            }

            $vars = __KrTestCallerArray
            $srvMeta = $vars | Where-Object Name -EQ 'srv'
            @($srvMeta).Count | Should -Be 1
            $srvMeta.Value.Name | Should -Be 'Demo'
            $srvMeta.Value.Value | Should -Be 123
        }

        It 'supports -FromParent with OutputStructure StringObjectMap' {
            function __KrTestCallerSOM {
                $srv = [pscustomobject]@{ Name = 'Demo'; Value = 123 }
                return Get-KrAssignedVariable -FromParent -ResolveValues -OutputStructure 'StringObjectMap'
            }

            $vars = __KrTestCallerSOM
            $vars.ContainsKey('srv') | Should -BeTrue
            $vars['srv'].Name | Should -Be 'Demo'
            $vars['srv'].Value | Should -Be 123
        }

        It 'respects -ExcludeVariables with OutputStructure Dictionary' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Dictionary' -ExcludeVariables @('skip') -ScriptBlock {
                $keep = 1
                $skip = 2
            }

            $dict.ContainsKey('keep') | Should -BeTrue
            $dict.ContainsKey('skip') | Should -BeFalse
        }

        It 'respects -IncludeSetVariable with OutputStructure StringObjectMap' {
            $dict = Get-KrAssignedVariable -ResolveValues -OutputStructure 'StringObjectMap' -IncludeSetVariable -ExcludeVariables @('hidden') -ScriptBlock {
                Set-Variable -Name 'hidden' -Value 1
                New-Variable -Name 'visible' -Value 2
            }

            $dict.ContainsKey('hidden') | Should -BeFalse
            $dict.ContainsKey('visible') | Should -BeTrue
        }

        It 'filters attributed variables with -WithoutAttributesOnly in Array output' {
            $vars = Get-KrAssignedVariable -ResolveValues -OutputStructure 'Array' -WithoutAttributesOnly -ScriptBlock {
                [int]$Port = 5000
                [ValidateNotNull()][int]$Foo = 1
            }

            ($vars.Name -contains 'Port') | Should -BeTrue
            ($vars.Name -contains 'Foo') | Should -BeFalse
        }
    }
}
