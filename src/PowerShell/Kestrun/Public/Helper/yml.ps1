[Flags()]
enum SerializationOptions {
    None = 0
    Roundtrip = 1
    DisableAliases = 2
    EmitDefaults = 4
    JsonCompatible = 8
    DefaultToStaticType = 16
    WithIndentedSequences = 32
    OmitNullValues = 64
    UseFlowStyle = 128
    UseSequenceFlowStyle = 256
}





function Convert-HashtableToDictionary {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [hashtable]$Data
    )
    foreach ($i in $($data.PSBase.Keys)) {
        $Data[$i] = Convert-PSObjectToGenericObject $Data[$i]
    }
    return $Data
}

function Convert-OrderedHashtableToDictionary {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [System.Collections.Specialized.OrderedDictionary] $Data
    )
    foreach ($i in $($data.PSBase.Keys)) {
        $Data[$i] = Convert-PSObjectToGenericObject $Data[$i]
    }
    return $Data
}

function Convert-ListToGenericList {
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [array]$Data = @()
    )
    $ret = [System.Collections.Generic.List[object]](New-Object "System.Collections.Generic.List[object]")
    for ($i = 0; $i -lt $Data.Count; $i++) {
        $ret.Add((Convert-PSObjectToGenericObject $Data[$i]))
    }
    return , $ret
}

function Convert-PSObjectToGenericObject {
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [System.Object]$Data
    )

    if ($null -eq $data) {
        return $data
    }

    $dataType = $data.GetType()
    if (([System.Collections.Specialized.OrderedDictionary].IsAssignableFrom($dataType))) {
        return Convert-OrderedHashtableToDictionary $data
    } elseif (([System.Collections.IDictionary].IsAssignableFrom($dataType))) {
        return Convert-HashtableToDictionary $data
    } elseif (([System.Collections.IList].IsAssignableFrom($dataType))) {
        return Convert-ListToGenericList $data
    }
    return $data
}

function ConvertFrom-Yaml {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true, Position = 0)]
        [string]$Yaml,
        [switch]$AllDocuments = $false,
        [switch]$Ordered,
        [switch]$UseMergingParser = $false
    )

    begin {
        $d = ""
    }
    process {
        if ($Yaml -is [string]) {
            $d += $Yaml + "`n"
        }
    }

    end {
        if ($d -eq "") {
            return
        }
        $documents = [Kestrun.Utilities.Yaml.YamlLoader]::GetYamlDocuments($d, $UseMergingParser)
        if (!$documents.Count) {
            return
        }
        if ($documents.Count -eq 1) {
            return [Kestrun.Utilities.Yaml.YamlTypeConverter]::ConvertYamlDocumentToPSObject($documents[0].RootNode, $Ordered) # single document
        }
        if (!$AllDocuments) {
            return [Kestrun.Utilities.Yaml.YamlTypeConverter]::ConvertYamlDocumentToPSObject($documents[0].RootNode, $Ordered)
        }
        $ret = @()
        foreach ($i in $documents) {
            $ret += [Kestrun.Utilities.Yaml.YamlTypeConverter]::ConvertYamlDocumentToPSObject($i.RootNode, $Ordered)
        }
        return $ret
    }
}

function Get-Serializer {
    param(
        [Parameter(Mandatory = $true)][SerializationOptions]$Options
    )

    $builder = $yamlDotNetAssembly.GetType("YamlDotNet.Serialization.SerializerBuilder")::new()
    $JsonCompatible = $Options.HasFlag([SerializationOptions]::JsonCompatible)

    if ($Options.HasFlag([SerializationOptions]::Roundtrip)) {
        $builder = $builder.EnsureRoundtrip()
    }
    if ($Options.HasFlag([SerializationOptions]::DisableAliases)) {
        $builder = $builder.DisableAliases()
    }
    if ($Options.HasFlag([SerializationOptions]::EmitDefaults)) {
        $builder = $builder.EmitDefaults()
    }
    if ($JsonCompatible) {
        $builder = $builder.JsonCompatible()
    }
    if ($Options.HasFlag([SerializationOptions]::DefaultToStaticType)) {
        $resolver = $yamlDotNetAssembly.GetType("YamlDotNet.Serialization.TypeResolvers.StaticTypeResolver")::new()
        $builder = $builder.WithTypeResolver($resolver)
    }
    if ($Options.HasFlag([SerializationOptions]::WithIndentedSequences)) {
        $builder = $builder.WithIndentedSequences()
    }

    $omitNull = $Options.HasFlag([SerializationOptions]::OmitNullValues)
    $useFlowStyle = $Options.HasFlag([SerializationOptions]::UseFlowStyle)
    $useSequenceFlowStyle = $Options.HasFlag([SerializationOptions]::UseSequenceFlowStyle)

    $stringQuoted = $stringQuotedAssembly.GetType("BuilderUtils")
    $builder = $stringQuoted::BuildSerializer($builder, $omitNull, $useFlowStyle, $useSequenceFlowStyle, $JsonCompatible)

    return $builder.Build()
}

function ConvertTo-Yaml {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'NoOptions')]
    param(
        [Parameter(ValueFromPipeline = $true, Position = 0)]
        [System.Object]$Data,

        [string]$OutFile,

        [Parameter(ParameterSetName = 'Options')]
        [SerializationOptions]$Options = [SerializationOptions]::Roundtrip,

        [Parameter(ParameterSetName = 'NoOptions')]
        [switch]$JsonCompatible,
        [switch]$UseFlowStyle,

        [switch]$KeepArray,

        [switch]$Force
    )
    begin {
        $d = [System.Collections.Generic.List[object]](New-Object "System.Collections.Generic.List[object]")
    }
    process {
        if ($data -is [System.Object]) {
            $d.Add($data)
        }
    }
    end {
        if ($d -eq $null -or $d.Count -eq 0) {
            return
        }
        if ($d.Count -eq 1 -and !($KeepArray)) {
            $d = $d[0]
        }
        $norm = Convert-PSObjectToGenericObject $d
        if ($OutFile) {
            $parent = Split-Path $OutFile
            if (!(Test-Path $parent)) {
                throw "Parent folder for specified path does not exist"
            }
            if ((Test-Path $OutFile) -and !$Force) {
                throw "Target file already exists. Use -Force to overwrite."
            }
            $wrt = New-Object "System.IO.StreamWriter" $OutFile
        } else {
            $wrt = New-Object "System.IO.StringWriter"
        }

        if ($PSCmdlet.ParameterSetName -eq 'NoOptions') {
            $Options = 0
            if ($JsonCompatible) {
                # No indent options :~(
                $Options = [SerializationOptions]::JsonCompatible
            }
        }

        try {
            $serializer = Get-Serializer $Options
            $serializer.Serialize($wrt, $norm)
        } catch {
            $_
        } finally {
            $wrt.Close()
        }
        if ($OutFile) {
            return
        } else {
            return $wrt.ToString()
        }
    }
}
