# Copyright 2016-2024 Cloudbase Solutions Srl
#
#    Licensed under the Apache License, Version 2.0 (the "License"); you may
#    not use this file except in compliance with the License. You may obtain
#    a copy of the License at
#
#         http://www.apache.org/licenses/LICENSE-2.0
#
#    Unless required by applicable law or agreed to in writing, software
#    distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
#    WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
#    License for the specific language governing permissions and limitations
#    under the License.
#

# pinning this module to an exact version,
# because the options api will be merged with Assert-Equivalent
# before release of 1.0.0

# Runtime feature / version tests for Kestrun PowerShell surface
# Mirrors C# unit tests for KestrunRuntimeInfo

BeforeAll {
    # Ensure module is imported (_.Tests.ps1 normally does this too, but be defensive)
    if (-not (Get-Module -Name Kestrun)) {
        $path = $PSCommandPath
        $kestrunPath = Join-Path -Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $path)))) -ChildPath 'src' -AdditionalChildPath 'PowerShell', 'Kestrun'
        if (Test-Path -Path "$kestrunPath/Kestrun.psm1" -PathType Leaf) {
            Import-Module "$kestrunPath/Kestrun.psm1" -Force -ErrorAction Stop
        } else {
            throw "Kestrun module not found at $kestrunPath"
        }
    }
    <#
    .SYNOPSIS
        Compare two objects deeply by converting them to JSON and comparing the strings.
    .DESCRIPTION
        This function takes two objects, converts them to JSON with a depth of 100, and compares the resulting JSON strings.
        It is useful for deep comparison of complex objects in tests.
    .PARAMETER Expected
        The expected object.
    .PARAMETER Actual
        The actual object to compare against the expected.
    .EXAMPLE
        $obj1 = @{ Key1 = "Value1"; Key2 = @{ SubKey = "SubValue" } }
        $obj2 = @{ Key1 = "Value1"; Key2 = @{ SubKey = "SubValue" } }
        Compare-Deep -Expected $obj1 -Actual $obj2
        # This will pass as the objects are deeply equivalent.
    #>
    function Compare-Deep {
        param(
            [Parameter()][AllowNull()]$Expected,
            [Parameter()][AllowNull()]$Actual
        )
        $expectedJson = ($Expected | ConvertTo-Json -Depth 99 -Compress).Replace("`r`n", "`n").Replace('\r\n', '\n')
        $actualJson = ($Actual | ConvertTo-Json -Depth 99 -Compress).Replace("`r`n", "`n").Replace('\r\n', '\n')
        $actualJson | Should -BeExactly $expectedJson
    }
}
Describe 'Yaml PowerShell Functions' {

    Describe "Test flow styles" {
        Context "Mappings, sequences and PSCustomObjects" {
            It "Should serialize Block flow (default) correctly" {
                $obj = [ordered]@{
                    aStringKey = "test"
                    anIntKey = 1
                    anArrayKey = @(1, 2, 3)
                }
                $expected = @"
aStringKey: test
anIntKey: 1
anArrayKey:
- 1
- 2
- 3

"@
                $serialized = ConvertTo-KrYaml $obj
                Compare-Deep -Expected $expected -Actual $serialized

                $pso = [pscustomobject]$obj
                $serialized = ConvertTo-KrYaml $pso
                Compare-Deep -Expected $expected -Actual $serialized
            }

            It "Should serialize Flow flow correctly" {
                $obj = [ordered]@{
                    aStringKey = "test"
                    anIntKey = 1
                    anArrayKey = @(1, 2, 3)
                }
                $expected = @"
{aStringKey: test, anIntKey: 1, anArrayKey: [1, 2, 3]}

"@
                $serialized = ConvertTo-KrYaml -Options UseFlowStyle $obj
                Compare-Deep -Expected $expected -Actual $serialized

                $pso = [pscustomobject]$obj
                $serialized = ConvertTo-KrYaml -Options UseFlowStyle $pso
                Compare-Deep -Expected $expected -Actual $serialized
            }

            It "Should serialize SequenceFlowStyle correctly" {
                $obj = [ordered]@{
                    aStringKey = "test"
                    anIntKey = 1
                    anArrayKey = @(1, 2, 3)
                }
                $expected = @"
aStringKey: test
anIntKey: 1
anArrayKey: [1, 2, 3]

"@
                $serialized = ConvertTo-KrYaml -Options UseSequenceFlowStyle $obj
                Compare-Deep -Expected $expected -Actual $serialized

                $pso = [pscustomobject]$obj
                $serialized = ConvertTo-KrYaml -Options UseSequenceFlowStyle $pso
                Compare-Deep -Expected $expected -Actual $serialized
            }

            It "Should serialize JsonCompatible correctly" {
                $obj = [ordered]@{
                    aStringKey = "test"
                    anIntKey = 1
                    anArrayKey = @(1, 2, 3)
                }
                $expected = @"
{"aStringKey": "test", "anIntKey": 1, "anArrayKey": [1, 2, 3]}

"@
                $serialized = ConvertTo-KrYaml -Options JsonCompatible $obj
                Compare-Deep -Expected $expected -Actual $serialized

                if ($PSVersionTable['PSEdition'] -eq 'Core') {
                    $deserializedWithJSonCommandlet = $serialized | ConvertFrom-Json -AsHashtable
                    Compare-Deep -Expected $obj -Actual $deserializedWithJSonCommandlet
                }

                $pso = [pscustomobject]$obj
                $serialized = ConvertTo-KrYaml -Options JsonCompatible $pso
                Compare-Deep -Expected $expected -Actual $serialized

                if ($PSVersionTable['PSEdition'] -eq 'Core') {
                    $deserializedWithJSonCommandlet = $serialized | ConvertFrom-Json -AsHashtable
                    Compare-Deep -Expected $obj -Actual $deserializedWithJSonCommandlet
                }
            }
        }
    }

    Describe "Test serialized depth" {
        Context "Deeply nested objects are serialized correctly" {
            It "Should deserialize the entire object" {
                $inputObject = @"
children:
  appliance:
    bla:
      bla2:
        bla3:
          bla4:
            bla5:
              bla6:
                bla7:
                  bla8:
                    bla9:
                      bla10:
                        bla11:
                          bla12:
                            bla13:
                              bla14:
                                bla15:
                                  bla16:
                                    bla17:
                                      bla18:
                                        bla19:
                                          bla20:
                                            bla21:
                                              bla22:
                                                bla23:
                                                  bla24:
                                                    bla25:
                                                      bla26:
                                                        bla27:
                                                          bla28:
                                                            bla29:
                                                              bla30:
                                                                bla31:
                                                                  bla32:
                                                                    bla33:
                                                                      bla34:
                                                                        bla35:
                                                                          bla36:
                                                                            bla37:
                                                                              bla38:
                                                                                bla39:
                                                                                  bla40:
                                                                                    bla41:
                                                                                      bla42:
                                                                                        bla43:
                                                                                          bla44:
                                                                                            bla45:
                                                                                              bla46:
                                                                                                bla47:
                                                                                                  bla48:
                                                                                                    bla49:
                                                                                                      bla50:
                                                                                                        bla51:
                                                                                                        - 192.168.0.1
                                                                                                        - 192.168.0.2
                                                                                                        - 192.168.0.3

"@
                $result = ConvertFrom-KrYaml $inputObject | ConvertTo-KrYaml
                Compare-Deep -Expected $inputObject -Actual $result
            }
        }
    }

    Describe "Test PSCustomObject wrapped values are serialized correctly" {
        Context "A PSCustomObject that contains an array of PSObjects" {
            It "Should serialize correctly" {
                $expected = @"
yamlList:
- item1
- item2

"@
                $inputObject = ConvertFrom-KrYaml "yamlList: []" | ConvertTo-Json -Depth 3 | ConvertFrom-Json
                $jsData = '["item1", "item2"]'
                $inputObject.yamlList = $jsData | ConvertFrom-Json

                $asYaml = ConvertTo-KrYaml $inputObject
                Compare-Deep -Expected $expected -Actual $asYaml
            }
        }
        Context "A PSCustomObject containing nested PSCustomObjects" {
            It "Should serialize correctly" {
                $expectBigInt = [System.Numerics.BigInteger]::Parse("9999999999999999999999999999999999999999999999999")
                $obj = [PSCustomObject]@{a = 'string'; b = 1; c = @{nested = $true }; d = [pscustomobject]$expectBigInt }
                $asYaml = ConvertTo-KrYaml $obj
                $fromYaml = ConvertFrom-KrYaml $asYaml

                Compare-Deep -Expected "string" -Actual $fromYaml["a"]
                Compare-Deep -Expected 1 -Actual $fromYaml["b"]
                Compare-Deep -Expected $expectBigInt -Actual $fromYaml["d"]
            }
        }

        Context "A hashtable containing nested PSCustomObjects" {
            It "Should serialize correctly" {
                $expectBigInt = [System.Numerics.BigInteger]::Parse("9999999999999999999999999999999999999999999999999")
                $obj = @{a = Write-Output 'string'; b = Write-Output 1; c = Write-Output @{nested = $true }; d = [pscustomobject]$expectBigInt }
                $asYaml = ConvertTo-KrYaml $obj
                $fromYaml = ConvertFrom-KrYaml $asYaml

                Compare-Deep -Expected "string" -Actual $fromYaml["a"]
                Compare-Deep -Expected 1 -Actual $fromYaml["b"]
                Compare-Deep -Expected $expectBigInt -Actual $fromYaml["d"]
            }
        }

        Context "A generic dictionary containing nested PSCustomObjects" {
            It "Should serialize correctly" {
                $expectBigInt = [System.Numerics.BigInteger]::Parse("9999999999999999999999999999999999999999999999999")
                $obj = [System.Collections.Generic.Dictionary[string, object]]::new()
                $obj["a"] = Write-Output 'string'
                $obj["b"] = Write-Output 1
                $obj["c"] = Write-Output @{nested = $true }
                $obj["d"] = [pscustomobject]$expectBigInt

                $asYaml = ConvertTo-KrYaml $obj
                $fromYaml = ConvertFrom-KrYaml $asYaml

                Compare-Deep -Expected "string" -Actual $fromYaml["a"]
                Compare-Deep -Expected 1 -Actual $fromYaml["b"]
                Compare-Deep -Expected $expectBigInt -Actual $fromYaml["d"]
            }
        }
    }

    Describe "Test encode-decode symmetry." {

        Context "Simple-Items" {
            It "Should represent identity to encode and decode." -TestCases @(
                @{ Expected = 1 }
                @{ Expected = "yes" }
                @{ Expected = 56 }
                @{ Expected = $null }
            ) {
                param ($Expected)
                $actual = ConvertFrom-KrYaml (ConvertTo-KrYaml $Expected)

                Compare-Deep -Expected $Expected -Actual $actual
            }
        }

        Context "Nulls and strings" {
            BeforeAll {
                $script:nullAndString = [ordered]@{"iAmNull" = $null; "iAmEmptyString" = "" }
                $script:yaml = @"
iAmNull:
iAmEmptyString: ""

"@
            }

            It "should not serialize null value when -Options OmitNullValues is set" {
                $toYaml = ConvertTo-KrYaml $nullAndString -Options OmitNullValues
                $toYaml | Should -Be "iAmEmptyString: """"$([Environment]::NewLine)"
            }

            It "should preserve nulls and empty strings from PowerShell" {
                $toYaml = ConvertTo-KrYaml $nullAndString
                $backFromYaml = ConvertFrom-KrYaml $toYaml

                ($null -eq $backFromYaml.iAmNull) | Should -Be $true
                $backFromYaml.iAmEmptyString | Should -Be ""
                $toYaml.Replace("`r`n", "`n").Replace('\r\n', '\n') | Should -Be $yaml.Replace("`r`n", "`n").Replace('\r\n', '\n')
            }

            It "should preserve nulls and empty strings from Yaml" {
                $fromYaml = ConvertFrom-KrYaml $yaml
                $backToYaml = ConvertTo-KrYaml $fromYaml

                $backToYaml.Replace("`r`n", "`n").Replace('\r\n', '\n') | Should -Be $yaml.Replace("`r`n", "`n").Replace('\r\n', '\n')
                ($null -eq $fromYaml.iAmNull) | Should -Be $true
                $fromYaml.iAmEmptyString | Should -Be ""
            }
        }

        Context "Test array handling under various circumstances." {
            $arr = 1, 2, "yes", @{ key = "value" }, 5, (1, "no", 3)

            It "Should represent identity to encode/decode arrays as arguments." {
                $yaml = ConvertTo-KrYaml $arr
                $a = ConvertFrom-KrYaml $yaml

                Compare-Deep -Actual $a -Expected $arr
            }

            It "Should represent identity to encode/decode arrays by piping them in." {
                $yaml = $arr | ConvertTo-KrYaml
                $a = ConvertFrom-KrYaml $yaml

                Compare-Deep -Actual $a -Expected $arr
            }

            It "Should be irrelevant whether we convert an array by piping it, or referencing them as an argument." {
                $arged = ConvertTo-KrYaml $arr
                $piped = $arr | ConvertTo-KrYaml

                Compare-Deep -Actual $piped -Expected $arged
            }
        }

        Context "Test merging parser" {
            BeforeAll {
                $script:mergingYaml = @"
---
default: &default
  value1: 1
  value2: 2

hoge:
  <<: *default
  value3: 3
"@

                $script:mergingYamlOverwriteCase = @"
---
default: &default
  value1: 1
  value2: 2

hoge:
  <<: *default
  value1: 33
  value3: 3
"@
            }

            It "Should expand merging key with appropriate referenced keys" {
                $result = ConvertFrom-KrYaml -Yaml $mergingYaml -UseMergingParser
                [array]$values = $result.hoge.keys
                [array]::sort($values)
                Compare-Deep -Actual $values -Expected @("value1", "value2", "value3")
            }

            It "Should retain literal key name in the absence of -UseMergingParser" {
                $result = ConvertFrom-KrYaml -Yaml $mergingYaml
                [array]$values = $result.hoge.keys
                [array]::sort($values)
                Compare-Deep -Actual $values -Expected @("<<", "value3")
            }

            It "Should Throw duplicate key exception when merging keys" {
                # This case does not seem to be treated by YamlDotNet and currently throws
                # a duplicate key exception
                { ConvertFrom-KrYaml -Yaml $mergingYamlOverwriteCase -UseMergingParser } | Should -Throw -PassThru | Select-Object -ExpandProperty Exception |
                    Should -BeLike "*Duplicate key*"
            }
        }

        Context "Test hash handling under various circumstances." {
            $hash = @{
                # NOTE: intentionally not considered as YAML requires dict keys
                # be strings. As such; decoding the encoding of this would result
                # in a hash with the string key of "1", as below:
                # 1 = 42;
                "1" = 42;
                today = @{
                    month = "January";
                    year = "2016";
                    timestamp = Get-Date
                };
                arr = 1, 2, 3, "yes", @{ yes = "yes" };
                yes = "no"
            }

            It "Should be symmetrical to encode and then decode the hash as an argument." {
                $yaml = ConvertTo-KrYaml $hash
                $h = ConvertFrom-KrYaml $yaml

                Compare-Deep -Actual $h -Expected $hash
            }

            It "Should be symmetrical to endocode and then decode a hash by piping it." {
                $yaml = $hash | ConvertTo-KrYaml
                $h = ConvertFrom-KrYaml $yaml

                Compare-Deep -Actual $h -Expected $hash
            }

            It "Shouldn't matter whether we reference or pipe our hashes in to the YAML functions." {
                $arged = ConvertTo-KrYaml $hash
                $piped = $hash | ConvertTo-KrYaml

                Compare-Deep -Actual $piped -Expected $arged
            }
        }
    }

    Describe "Being able to decode an externally provided string." {

        Context "Decoding an arbitrary YAML string correctly." {
            BeforeAll {
                # testYaml is just a string containing some yaml to be tested below:
                $testYaml = @"
wishlist:
    - [coats, hats, and, scarves]
    - product     : A Cool Book.
      quantity    : 1
      description : I love that Cool Book.
      price       : 55.34
total: 4443.52
int64: $([int64]::MaxValue)
note: >
    I can't wait.
    To get that Cool Book.

intsAndDecimals:
    aStringTatLooksLikeAFloat: 55,34
    aStringThatLooksLikeAnInt: 2018+
    scientificNotationInt: 1e+3
    scientificNotationBigInt: 1e+40
    intWithTag: !!int "42"
    zeroIntWithTag: !!int "0"
    zeroIntWithoutTag: 0
    scientificNotationIntWithTag: !!int "1e+3"
    aDecimalWithATag: !!float "3.9999999999999990"
    aDecimalWithoutATag: 3.9999999999999990
    decimalInfinity: !!float ".inf"
    decimalNegativeInfinity: !!float "-.inf"

dates:
    - !!timestamp 2001-12-15T02:59:43.1Z
    - !!timestamp 2001-12-14t21:59:43.10-05:00
    - !!timestamp 2001-12-14 21:59:43.10 -5
    - !!timestamp 2001-12-15 2:59:43.10
    - !!timestamp 2002-12-14
datesAsStrings:
    - 2001-12-15T02:59:43.1Z
    - 2001-12-14t21:59:43.10-05:00
    - 2001-12-14 21:59:43.10 -5
    - 2001-12-15 2:59:43.10
    - 2002-12-14

version:
    - 1.2.3
noniso8601dates:
    - 5/4/2017
    - 1.2.3
bools:
    - true
    - false
    - TRUE
    - FALSE
    - True
    - False
"@

                $script:expected = [ordered]@{
                    wishlist = @(
                        @("coats", "hats", "and", "scarves"),
                        [ordered]@{
                            product = "A Cool Book.";
                            quantity = 1;
                            description = "I love that Cool Book.";
                            price = 55.34;
                        }
                    );
                    total = 4443.52;
                    int64 = ([int64]::MaxValue);
                    note = ("I can't wait. To get that Cool Book.`n");
                    intsAndDecimals = [ordered]@{
                        aStringTatLooksLikeAFloat = "55,34";
                        aStringThatLooksLikeAnInt = "2018+"
                        scientificNotationInt = [int32]1000
                        scientificNotationBigInt = [System.Numerics.BigInteger]::Parse("10000000000000000000000000000000000000000")
                        intWithTag = 42
                        zeroIntWithTag = 0
                        zeroIntWithoutTag = 0
                        scientificNotationIntWithTag = 1000
                        aDecimalWithATag = [decimal]::Parse("3.9999999999999990", [System.Globalization.CultureInfo]::InvariantCulture)
                        aDecimalWithoutATag = [decimal]::Parse("3.9999999999999990", [System.Globalization.CultureInfo]::InvariantCulture)
                        decimalInfinity = [double]::PositiveInfinity
                        decimalNegativeInfinity = [double]::NegativeInfinity
                    }

                    dates = @(
                        [DateTime]::Parse('2001-12-15T02:59:43.1Z'),
                        [DateTime]::Parse('2001-12-14t21:59:43.10-05:00'),
                        [DateTime]::Parse('2001-12-14 21:59:43.10 -5'),
                        [DateTime]::Parse('2001-12-15 2:59:43.10'),
                        [DateTime]::Parse('2002-12-14')
                    );
                    datesAsStrings = @(
                        "2001-12-15T02:59:43.1Z",
                        "2001-12-14t21:59:43.10-05:00",
                        "2001-12-14 21:59:43.10 -5",
                        "2001-12-15 2:59:43.10",
                        "2002-12-14"
                    );
                    version = "1.2.3";
                    noniso8601dates = @( '5/4/2017', '1.2.3' );
                    bools = @( $true, $false, $true, $false, $true, $false );
                }

                $script:res = ConvertFrom-KrYaml $testYaml
            }

            It "Should decode the YAML string as expected." {
                $wishlist = $res['wishlist']
                $wishlist | Should -Not -BeNullOrEmpty
                $wishlist.Count | Should -Be 2
                $wishlist[0] | Should -Not -BeNullOrEmpty
                $wishlist[0].Count | Should -Be 4
                $wishlist[0][0] | Should -Be $expected['wishlist'][0][0]
                $wishlist[0][1] | Should -Be $expected['wishlist'][0][1]
                $wishlist[0][2] | Should -Be $expected['wishlist'][0][2]
                $wishlist[0][3] | Should -Be $expected['wishlist'][0][3]
                $product = $res['wishlist'][1]
                $product | Should -Not -BeNullOrEmpty
                $expectedProduct = $expected['wishlist'][1]
                $product['product'] | Should -Be $expectedProduct['product']
                $product['quantity'] | Should -Be $expectedProduct['quantity']
                $product['description'] | Should -Be $expectedProduct['description']
                $product['price'] | Should -Be $expectedProduct['price']

                $res['total'] | Should -Be $expected['total']
                $res['note'] | Should -Be $expected['note']

                $expectedIntsAndDecimals = $expected['intsAndDecimals']

                $intsAndDecimals = $res['intsAndDecimals']
                $intsAndDecimals['aStringTatLooksLikeAFloat'] | Should -Be $expectedIntsAndDecimals['aStringTatLooksLikeAFloat']
                $intsAndDecimals['aStringTatLooksLikeAFloat'] | Should -BeOfType ([string])
                $intsAndDecimals['aStringThatLooksLikeAnInt'] | Should -Be $expectedIntsAndDecimals['aStringThatLooksLikeAnInt']
                $intsAndDecimals['aStringThatLooksLikeAnInt'] | Should -BeOfType ([string])
                $intsAndDecimals['zeroIntWithTag'] | Should -Be $expectedIntsAndDecimals['zeroIntWithTag']
                $intsAndDecimals['zeroIntWithTag'] | Should -BeOfType ([int32])
                $intsAndDecimals['zeroIntWithoutTag'] | Should -Be $expectedIntsAndDecimals['zeroIntWithoutTag']
                $intsAndDecimals['zeroIntWithoutTag'] | Should -BeOfType ([int32])
                $intsAndDecimals['scientificNotationInt'] | Should -Be $expectedIntsAndDecimals['scientificNotationInt']
                $intsAndDecimals['scientificNotationInt'] | Should -BeOfType ([int32])
                $intsAndDecimals['scientificNotationBigInt'] | Should -Be $expectedIntsAndDecimals['scientificNotationBigInt']
                $intsAndDecimals['scientificNotationBigInt'] | Should -BeOfType ([System.Numerics.BigInteger])
                $intsAndDecimals['intWithTag'] | Should -Be $expectedIntsAndDecimals['intWithTag']
                $intsAndDecimals['intWithTag'] | Should -BeOfType ([int32])
                $intsAndDecimals['scientificNotationIntWithTag'] | Should -Be $expectedIntsAndDecimals['scientificNotationIntWithTag']
                $intsAndDecimals['scientificNotationIntWithTag'] | Should -BeOfType ([int32])
                $intsAndDecimals['aDecimalWithATag'] | Should -Be $expectedIntsAndDecimals['aDecimalWithATag']
                $intsAndDecimals['aDecimalWithATag'] | Should -BeOfType ([decimal])
                $intsAndDecimals['aDecimalWithoutATag'] | Should -Be $expectedIntsAndDecimals['aDecimalWithoutATag']
                $intsAndDecimals['aDecimalWithoutATag'] | Should -BeOfType ([decimal])
                $intsAndDecimals['decimalInfinity'] | Should -Be $expectedIntsAndDecimals['decimalInfinity']
                $intsAndDecimals['decimalInfinity'] | Should -BeOfType ([double])
                $intsAndDecimals['decimalNegativeInfinity'] | Should -Be $expectedIntsAndDecimals['decimalNegativeInfinity']
                $intsAndDecimals['decimalNegativeInfinity'] | Should -BeOfType ([double])

                $res['dates'] | Should -Not -BeNullOrEmpty
                $res['dates'].Count | Should -Be $expected['dates'].Count
                for ( $idx = 0; $idx -lt $expected['dates'].Count; ++$idx ) {
                    $res['dates'][$idx] | Should -BeOfType ([datetime])
                    $res['dates'][$idx] | Should -Be $expected['dates'][$idx]
                }

                $res['datesAsStrings'] | Should -Not -BeNullOrEmpty
                $res['datesAsStrings'].Count | Should -Be $expected['datesAsStrings'].Count
                for ( $idx = 0; $idx -lt $expected['datesAsStrings'].Count; ++$idx ) {
                    $res['datesAsStrings'][$idx] | Should -BeOfType ([string])
                    $res['datesAsStrings'][$idx] | Should -Be $expected['datesAsStrings'][$idx]
                }

                $res['version'] | Should -BeOfType ([string])
                $res['version'] | Should -Be $expected['version']

                $res['noniso8601dates'] | Should -Not -BeNullOrEmpty
                $res['noniso8601dates'].Count | Should -Be $expected['noniso8601dates'].Count
                for ( $idx = 0; $idx -lt $expected['noniso8601dates'].Count; ++$idx ) {
                    $res['noniso8601dates'][$idx] | Should -BeOfType ([string])
                    $res['noniso8601dates'][$idx] | Should -Be $expected['noniso8601dates'][$idx]
                }

                Compare-Deep -Actual $res -Expected $expected
            }
        }
    }

    Describe "Test ConvertTo-KrYaml can serialize more complex nesting" {
        BeforeAll {
            $script:sample = [PSCustomObject]@{
                a1 = "a"
                a2 = [PSCustomObject]@{
                    "a1" = "a"
                    a2 = [PSCustomObject]@{
                        a1 = [PSCustomObject]@{
                            "a1" = "a"
                            a2 = [PSCustomObject]@{
                                a1 = "a"
                            }
                            a3 = [ordered]@{
                                a1 = @("a", "b")
                            }
                            a4 = @("a", "b")
                        }
                    }
                    a3 = @(
                        [PSCustomObject]@{
                            a1 = "a"
                            a2 = $False
                        }
                    )
                }
            }

            $script:sample2 = [PSCustomObject]@{
                b1 = "b"
                b2 = [PSCustomObject]@{
                    b1 = "b"
                    b2 = [PSCustomObject]@{
                        "b" = "b"
                    }
                }
                b3 = [ordered]@{
                    b1 = @("b1", "b2")
                }
                b4 = $True
                b5 = [PSCustomObject]@{
                    b = "b"
                }
            }

            $script:expected_json = '{"a1":"a","a2":{"a1":"a","a2":{"a1":{"a1":"a","a2":{"a1":"a"},"a3":{"a1":["a","b"]},"a4":["a","b"]}},"a3":[{"a1":"a","a2":false}]}}'
            $script:expected_json_ln = $script:expected_json + "$([Environment]::NewLine)"

            $script:expected_json2 = '{"b1":"b","b2":{"b1":"b","b2":{"b":"b"}},"b3":{"b1":["b1","b2"]},"b4":true,"b5":{"b":"b"}}'
            $script:expected_json2_ln = $script:expected_json2 + "$([Environment]::NewLine)"
            $script:expected_block_yaml = @"
a1: a
a2:
  a1: a
  a2:
    a1:
      a1: a
      a2:
        a1: a
      a3:
        a1:
        - a
        - b
      a4:
      - a
      - b
  a3:
  - a1: a
    a2: false

"@

            $script:expected_flow_yaml = "{a1: a, a2: {a1: a, a2: {a1: {a1: a, a2: {a1: a}, a3: {a1: [a, b]}, a4: [a, b]}}, a3: [{a1: a, a2: false}]}}$([Environment]::NewLine)"
            $script:expected_block_yaml2 = @"
b1: b
b2:
  b1: b
  b2:
    b: b
b3:
  b1:
  - b1
  - b2
b4: true
b5:
  b: b

"@
            $script:expected_flow_yaml2 = "{b1: b, b2: {b1: b, b2: {b: b}}, b3: {b1: [b1, b2]}, b4: true, b5: {b: b}}$([Environment]::NewLine)"
        }

        It "Should serialize nested PSCustomObjects to YAML" {
            $yaml = ConvertTo-KrYaml $sample
            Compare-Deep -Expected $expected_block_yaml -Actual $yaml

            $yaml = ConvertTo-KrYaml $sample2
            Compare-Deep -Expected $expected_block_yaml2 -Actual $yaml
        }

        It "Should serialize nested PSCustomObjects to YAML flow format" {
            $yaml = ConvertTo-KrYaml $sample -Options UseFlowStyle
            Compare-Deep -Expected $expected_flow_yaml -Actual $yaml

            $yaml = ConvertTo-KrYaml $sample2 -Options UseFlowStyle
            Compare-Deep -Expected $expected_flow_yaml2 -Actual $yaml
        }

        It "Should serialize nested PSCustomObjects to JSON" {
            # Converted with powershell-yaml
            $json = ConvertTo-KrYaml $sample -Options JsonCompatible
            $json.Replace(' ', '').Replace("`r`n", "`n").Replace('\r\n', '\n') | Should -Be $expected_json_ln.Replace("`r`n", "`n").Replace('\r\n', '\n')

            # Converted with ConvertTo-Json
            $withJsonCommandlet = ConvertTo-Json -Compress -Depth 99 $sample
            $withJsonCommandlet | Should -Be $expected_json

            # Converted with powershell-yaml
            $json = ConvertTo-KrYaml $sample2 -Options JsonCompatible
            $json -replace ' ', '' | Should -Be $expected_json2_ln

            # Converted with ConvertTo-Json
            $withJsonCommandlet = ConvertTo-Json -Compress -Depth 99 $sample2
            $withJsonCommandlet | Should -Be $expected_json2
        }
    }

    Describe "Generic Casting Behaviour" {
        Context "Node Style is 'Plain'" {
            BeforeAll {
                $script:value = @'
 T1: 001
'@
            }

            It 'Should be an int' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -BeOfType System.Int32
            }

            It 'Should be value of 1' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -Be 1
            }

            It 'Should not be value of 001' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -Not -Be '001'
            }
        }

        Context "Node Style is 'SingleQuoted'" {
            BeforeAll {
                $script:value = @'
 T1: '001'
'@
            }

            It 'Should be a string' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -BeOfType System.String
            }

            It 'Should be value of 001' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -Be '001'
            }

            It 'Should not be value of 1' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -Not -Be '1'
            }
        }

        Context "Node Style is 'DoubleQuoted'" {
            BeforeAll {
                $script:value = @'
 T1: "001"
'@
            }

            It 'Should be a string' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -BeOfType System.String
            }

            It 'Should be value of 001' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -Be '001'
            }

            It 'Should not be value of 1' {
                $result = ConvertFrom-KrYaml -Yaml $value
                $result.T1 | Should -Not -Be '1'
            }
        }
    }

    Describe 'Strings containing other primitives' {
        Context 'String contains an int' {
            BeforeAll {
                $script:value = @{key = "1" }
            }
            It 'Should serialise with double quotes' {
                $result = ConvertTo-KrYaml $value
                $result | Should -BeExactly "key: ""1""$([Environment]::NewLine)"
            }
        }
        Context 'String contains a float' {
            BeforeAll {
                $script:value = @{key = "0.25" }
            }
            It 'Should serialise with double quotes' {
                $result = ConvertTo-KrYaml $value
                $result | Should -BeExactly "key: ""0.25""$([Environment]::NewLine)"
            }
        }
        Context 'String is "true"' {
            BeforeAll {
                $script:value = @{key = "true" }
            }
            It 'Should serialise with double quotes' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key: ""true""$([Environment]::NewLine)"
            }
        }
        Context 'String is "false"' {
            BeforeAll {
                $script:value = @{key = "false" }
            }
            It 'Should serialise with double quotes' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key: ""false""$([Environment]::NewLine)"
            }
        }
        Context 'String is "null"' {
            BeforeAll {
                $script:value = @{key = "null" }
            }
            It 'Should serialise with double quotes' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key: ""null""$([Environment]::NewLine)"
            }
        }
        Context 'String is "~" (alternative syntax for null)' {
            BeforeAll {
                $script:value = @{key = "~" }
            }
            It 'Should serialise with double quotes' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key: ""~""$([Environment]::NewLine)"
            }
        }
        Context 'String is empty' {
            BeforeAll {
                $script:value = @{key = "" }
            }
            It 'Should serialise with double quotes' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key: """"$([Environment]::NewLine)"
            }
        }
    }

    Describe 'Numbers are parsed as the smallest type possible' {
        BeforeAll {
            $script:value = @'
bigInt: 99999999999999999999999999999999999
int32: 2147483647
int64: 9223372036854775807
decimal: 3.10
reallyLongDecimal: 3.9999999999999990
'@
        }

        It 'Should be a BigInt' {
            $result = ConvertFrom-KrYaml -Yaml $value
            $result.bigInt | Should -BeOfType System.Numerics.BigInteger
        }

        It "Should round-trip decimals with trailing 0" {
            $result = ConvertFrom-KrYaml -Yaml $value
            $result.decimal | Should -Be ([decimal]3.10)
            $result.reallyLongDecimal | Should -Be ([decimal]::Parse("3.9999999999999990", [cultureinfo]::InvariantCulture))

            ConvertTo-KrYaml $result["decimal"] | Should -Be "3.10$([Environment]::NewLine)"
            ConvertTo-KrYaml $result["reallyLongDecimal"] | Should -Be "3.9999999999999990$([Environment]::NewLine)"
        }

        It 'Should be of proper type and value' {
            $result = ConvertFrom-KrYaml -Yaml $value
            $result.bigInt | Should -Be ([System.Numerics.BigInteger]::Parse("99999999999999999999999999999999999"))
            $result.int32 | Should -Be ([int32]2147483647)
            $result.int64 | Should -Be ([int64]9223372036854775807)
            $result.decimal | Should -Be ([decimal]3.10)
        }
    }

    Describe 'PSCustomObjects' {
        Context 'Classes with PSCustomObjects' {
            It 'Should serialise as a hash' {
                $nestedPsO = [PSCustomObject]@{
                    Nested = 'NestedValue'
                }
                $nestedHashTable = @{
                    "aKey" = $nestedPsO
                }
                $nestedArray = @(
                    $nestedPsO, 1
                )
                $PsO = [PSCustomObject]@{
                    Name = 'Value'
                    Nested = $nestedPsO
                    NestedHashTable = $nestedHashTable
                    NestedArray = $nestedArray
                    NullValue = $null
                }

                class TestClass {
                    [PSCustomObject]$PsO
                    [string]$Ok
                }
                $Class = [TestClass]@{
                    PsO = $PsO
                    Ok = 'aye'
                }
                $asYaml = ConvertTo-KrYaml $Class
                $result = ConvertFrom-KrYaml -Yaml $asYaml
                [System.Collections.Specialized.OrderedDictionary]$ret = [System.Collections.Specialized.OrderedDictionary]::new()
                $ret["PsO"] = [System.Collections.Specialized.OrderedDictionary]::new()
                $ret["PsO"]["Name"] = "Value"
                $ret["PsO"]["Nested"] = [System.Collections.Specialized.OrderedDictionary]::new()
                $ret["PsO"]["Nested"]["Nested"] = "NestedValue"
                $ret["PsO"]["NestedHashTable"] = [ordered]@{
                    "aKey" = [ordered]@{
                        "Nested" = "NestedValue"
                    }
                }
                $ret["PsO"]["NestedArray"] = @(
                    [ordered]@{
                        "Nested" = "NestedValue"
                    }, 1
                )
                $ret["PsO"]["NullValue"] = $null
                $ret["Ok"] = "aye"
                Compare-Deep -Expected $ret -Actual $result
            }
        }

        Context 'PSObject with null value is skipped when -Options OmitNullValues' {
            BeforeAll {
                $script:value = [PSCustomObject]@{
                    key1 = "value1"
                    key2 = $null
                }
            }
            It 'Should serialise as a hash with only the non-null value' {
                $result = ConvertTo-KrYaml $value -Options OmitNullValues
                $result | Should -Be "key1: value1$([Environment]::NewLine)"
            }
        }

        Context 'PSObject with null value is included when -Options OmitNullValues is not set' {
            BeforeAll {
                $script:value = [PSCustomObject]@{
                    key1 = "value1"
                    key2 = $null
                }
            }
            It 'Should serialise as a hash with the null value' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key1: value1$([Environment]::NewLine)key2: null$([Environment]::NewLine)"
            }
        }

        Context 'PSCustomObject with a single property' {
            BeforeAll {
                $script:value = [PSCustomObject]@{key = "value" }
            }
            It 'Should serialise as a hash' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key: value$([Environment]::NewLine)"
            }
        }
        Context 'PSCustomObject with multiple properties' {
            BeforeAll {
                $script:value = [PSCustomObject]@{key1 = "value1"; key2 = "value2" }
            }
            It 'Should serialise as a hash' {
                $result = ConvertTo-KrYaml $value
                $result | Should -Be "key1: value1$([Environment]::NewLine)key2: value2$([Environment]::NewLine)"
            }
            It 'Should deserialise as a hash' {
                $asYaml = ConvertTo-KrYaml $value
                $result = ConvertFrom-KrYaml -Yaml $asYaml
                Compare-Deep -Expected ([ordered]@{key1 = "value1"; key2 = "value2" }) -Actual $result
            }
        }
    }
}
