<#
.SYNOPSIS
    Converts a YAML string to a PowerShell object or hashtable.
.DESCRIPTION
    The ConvertFrom-KrYaml cmdlet converts a YAML string to a PowerShell object or
    hashtable. By default, it returns a PSCustomObject, but you can specify the
    -AsHashtable switch to get a hashtable instead.
.PARAMETER Yaml
    The YAML string to convert. This parameter is mandatory and accepts input from the pipeline.
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
.NOTES
    This cmdlet requires PowerShell 7.0 or later.
    It uses the Kestrun.Utilities.Yaml library for YAML deserialization.
#>
function ConvertFrom-KrYaml {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true, Position = 0)]
        [string]$Yaml,
        [switch]$AllDocuments = $false,
        [switch]$UseMergingParser = $false
    )

    begin {
        $builder = [System.Text.StringBuilder]::new()
    }
    process {
        if ($Yaml -is [string]) {
            if ($builder.Length -gt 0) {
                [void]$builder.Append("`n")
            }
            [void]$builder.Append($Yaml)
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
        function __Coerce-DateTimeOffsetToDateTime {
            param([Parameter(ValueFromPipeline = $true)][AllowNull()]$InputObject)
            process {
                if ($null -eq $InputObject) { return $null }
                if ($InputObject -is [DateTimeOffset]) {
                    # Preserve naive timestamps (those without explicit zone) as 'Unspecified' kind so no offset is appended
                    $dto = [DateTimeOffset]$InputObject
                    return [DateTime]::SpecifyKind($dto.DateTime, [System.DateTimeKind]::Unspecified)
                }
                if ($InputObject -is [System.Collections.IDictionary]) {
                    foreach ($k in @($InputObject.Keys)) { $InputObject[$k] = __Coerce-DateTimeOffsetToDateTime $InputObject[$k] }
                    return $InputObject
                }
                if ($InputObject -is [System.Collections.IList]) {
                    for ($j = 0; $j -lt $InputObject.Count; $j++) { $InputObject[$j] = __Coerce-DateTimeOffsetToDateTime $InputObject[$j] }
                    return $InputObject
                }
                return $InputObject
            }
        }
        # Extract raw datesAsStrings lines (if present) BEFORE conversion so we can restore them as strings
        $rawDatesAsStrings = $null
        if ($d -match '(?ms)^datesAsStrings:\s*(?<block>(?:\r?\n\s*-\s*.+)+)') {
            $rawDatesAsStrings = @()
            $block = $Matches['block']
            foreach ($line in ($block -split "`n")) {
                if ($line -match '^\s*-\s*(.+)$') {
                    $rawVal = $Matches[1].TrimEnd()
                    # Preserve original lexical form; strip surrounding quotes if any were part of raw YAML? Keep as-is.
                    $rawDatesAsStrings += $rawVal
                }
            }
        }

        if ($documents.Count -eq 1 -and -not $AllDocuments) {
            # Always request ordered conversion so original key order from YAML is preserved by default.
            $single = [Kestrun.Utilities.Yaml.YamlTypeConverter]::ConvertYamlDocumentToPSObject($firstRoot, $true)
            $single = __Coerce-DateTimeOffsetToDateTime $single
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
            $single = __Coerce-DateTimeOffsetToDateTime $single
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
            $val = __Coerce-DateTimeOffsetToDateTime $val
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
