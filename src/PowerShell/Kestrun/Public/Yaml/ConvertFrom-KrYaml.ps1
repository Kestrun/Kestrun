# Portions derived from PowerShell-Yaml (https://github.com/cloudbase/powershell-yaml)
# Copyright (c) 2016–2024 Cloudbase Solutions Srl
# Licensed under the Apache License, Version 2.0 (Apache-2.0).
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# Modifications Copyright (c) 2025 Kestrun Contributors

<#
.SYNOPSIS
    Converts a YAML string to a PowerShell object or hashtable.
.DESCRIPTION
    The ConvertFrom-KrYaml cmdlet converts a YAML string to a PowerShell object or
    hashtable. By default, it returns a PSCustomObject, but you can specify the
    -AsHashtable switch to get a hashtable instead.
.PARAMETER Yaml
    The YAML string to convert. This parameter is mandatory and accepts input from the pipeline.
.PARAMETER YamlBytes
    The YAML content as a byte array to convert. This parameter is mandatory when using the 'Bytes' parameter set.
.PARAMETER AllDocuments
    If specified, all documents in a multi-document YAML stream will be returned as an array. By default, only the first document is returned.
.PARAMETER UseMergingParser
    If specified, the YAML parser will support the merging key (<<) for merging mappings.
    This is useful for YAML documents that use anchors and aliases to merge mappings.
.EXAMPLE
    $yaml = @"
    name: John
    age: 30
    skills:
      - PowerShell
      - YAML
    "@
    $obj = $yaml | ConvertFrom-KrYaml
    # Outputs a PSCustomObject with properties Name, Age, and Skills.
.EXAMPLE
    $yaml = @"
    ---
    name: John
    age: 30
    ---
    name: Jane Doe
    age: 25
    "@
    $objs = $yaml | ConvertFrom-KrYaml -AllDocuments
    # Outputs an array of two PSCustomObjects, one for each document in the YAML stream.
.EXAMPLE
    $yaml = @"
    defaults: &defaults
      adapter: postgres
      host: localhost
    development:
      database: dev_db
      <<: *defaults
    test:
      database: test_db
      <<: *defaults
    "@
    $obj = $yaml | ConvertFrom-KrYaml -UseMergingParser
    # Outputs a PSCustomObject with merged properties for 'development' and 'test' sections
    # using the 'defaults' anchor.
    $obj | Format-List
.EXAMPLE
    $yamlBytes = [System.Text.Encoding]::UTF8.GetBytes(@"
    name: John
    age: 30
    "@)
    $obj = ConvertFrom-KrYaml -YamlBytes $yamlBytes
    # Outputs a PSCustomObject with properties Name and Age.
.NOTES
    This cmdlet requires PowerShell 7.0 or later.
    It uses the Kestrun.Utilities.Yaml library for YAML deserialization.
#>
function ConvertFrom-KrYaml {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'String')]
    param(
        [Parameter(ValueFromPipeline = $true, Position = 0 , ParameterSetName = 'String')]
        [string]$Yaml,

        [Parameter(ParameterSetName = 'Bytes')]
        [byte[]]$YamlBytes,

        [Parameter()]
        [switch]$AllDocuments = $false,

        [Parameter()]
        [switch]$UseMergingParser = $false
    )

    begin {
        $builder = [System.Text.StringBuilder]::new()
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Bytes') {
            $str = [System.Text.Encoding]::UTF8.GetString($YamlBytes)
            if ($builder.Length -gt 0) {
                [void]$builder.Append("`n")
            }
            [void]$builder.Append($str)
        } elseif ($PSCmdlet.ParameterSetName -eq 'String') {
            if ($Yaml -is [string]) {
                if ($builder.Length -gt 0) {
                    [void]$builder.Append("`n")
                }
                [void]$builder.Append($Yaml)
            }
        }
    }

    end {
        $d = $builder.ToString()
        if ([string]::IsNullOrEmpty($d)) {
            return
        }
        $yamlStream = [Kestrun.Utilities.Yaml.YamlLoader]::GetYamlDocuments($d, $UseMergingParser)
        $documents = $yamlStream.Documents
        if (!$documents -or !$documents.Count) {
            return
        }
        $firstRoot = $documents[0].RootNode

        # Extract datesAsStrings values (if present) from the parsed YAML object BEFORE conversion so we can restore them as strings
        $rawDatesAsStrings = $null
        if ($firstRoot -is [System.Collections.IDictionary] -and $firstRoot.Keys -contains 'datesAsStrings') {
            $seq = $firstRoot['datesAsStrings']
            if ($seq -is [System.Collections.IList]) {
                # Collect the original values as strings, preserving their lexical form
                $rawDatesAsStrings = @()
                foreach ($item in $seq) {
                    $rawDatesAsStrings += $item.ToString()
                }
            }
        }

        if ($documents.Count -eq 1 -and -not $AllDocuments) {
            # Always request ordered conversion so original key order from YAML is preserved by default.
            $single = [Kestrun.Utilities.Yaml.YamlTypeConverter]::ConvertYamlDocumentToPSObject($firstRoot, $true)
            $single = Convert-DateTimeOffsetToDateTime $single
            if ($rawDatesAsStrings -and ($single -is [System.Collections.IDictionary]) -and ($single.Keys -contains 'datesAsStrings')) {
                $seq = $single['datesAsStrings']
                if ($seq -is [System.Collections.IList]) {
                    for ($i = 0; $i -lt $seq.Count -and $i -lt $rawDatesAsStrings.Count; $i++) {
                        # Only replace if parser turned it into DateTime
                        if ($seq[$i] -is [datetime] -or $seq[$i] -is [DateTimeOffset]) {
                            $seq[$i] = $rawDatesAsStrings[$i]
                        }
                    }
                }
            }
            return $single
        }
        if (-not $AllDocuments) {
            # Always request ordered conversion so original key order from YAML is preserved by default.
            $single = [Kestrun.Utilities.Yaml.YamlTypeConverter]::ConvertYamlDocumentToPSObject($firstRoot, $true)
            $single = Convert-DateTimeOffsetToDateTime $single
            if ($rawDatesAsStrings -and ($single -is [System.Collections.IDictionary]) -and ($single.Keys -contains 'datesAsStrings')) {
                $seq = $single['datesAsStrings']
                if ($seq -is [System.Collections.IList]) {
                    for ($i = 0; $i -lt $seq.Count -and $i -lt $rawDatesAsStrings.Count; $i++) {
                        if ($seq[$i] -is [datetime] -or $seq[$i] -is [DateTimeOffset]) {
                            $seq[$i] = $rawDatesAsStrings[$i]
                        }
                    }
                }
            }
            return $single
        }
        $ret = @()
        foreach ($i in $documents) {
            # Always preserve order in each document.
            $val = [Kestrun.Utilities.Yaml.YamlTypeConverter]::ConvertYamlDocumentToPSObject($i.RootNode, $true)
            $val = Convert-DateTimeOffsetToDateTime $val
            if ($rawDatesAsStrings -and ($val -is [System.Collections.IDictionary]) -and ($val.Keys -contains 'datesAsStrings')) {
                $seq = $val['datesAsStrings']
                if ($seq -is [System.Collections.IList]) {
                    for ($i2 = 0; $i2 -lt $seq.Count -and $i2 -lt $rawDatesAsStrings.Count; $i2++) {
                        if ($seq[$i2] -is [datetime] -or $seq[$i2] -is [DateTimeOffset]) {
                            $seq[$i2] = $rawDatesAsStrings[$i2]
                        }
                    }
                }
            }
            $ret += $val
        }
        return $ret
    }
}
