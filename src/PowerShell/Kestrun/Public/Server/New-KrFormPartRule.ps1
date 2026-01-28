<#
.SYNOPSIS
    Creates a new form part rule.
.DESCRIPTION
    This function creates and adds a form part rule to the server's form options collection.
    It allows you to specify various parameters for the form part rule, such as name, description,
    allowed content types, maximum size, and storage options.
.PARAMETER Rules
    An array of existing form part rules to which the new rule will be added.
.PARAMETER Name
    The name of the form part rule.
.PARAMETER Description
    A description of the form part rule.
.PARAMETER Required
    Indicates whether the form part is required.
.PARAMETER  AllowOnlyOne
    Indicates whether only one instance of the form part is allowed.
.PARAMETER AllowedContentTypes
    An array of allowed content types for the form part.
.PARAMETER AllowedExtensions
    An array of allowed file extensions for the form part.
.PARAMETER MaxBytes
    The maximum size in bytes for the form part.
.PARAMETER DecodeMode
    The decode mode for the form part.
.PARAMETER DestinationPath
    The destination path where the form part should be stored.
.PARAMETER StoreToDisk
    Indicates whether the form part should be stored to disk.
.EXAMPLE
    New-KrFormPartRule -Name 'file' -Required -AllowedContentTypes 'text/plain' -MaxBytes 1048576
    This example adds a form part rule named 'file' that is required, allows only 'text/plain' content type,
    and has a maximum size of 1 MB.
.NOTES
    This function is part of the Kestrun.Forms module and is used to define form part rules.
#>
function New-KrFormPartRule {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Forms.KrFormPartRule[]])]
    [OutputType([System.Array])]
    param(
        [Parameter(ValueFromPipeline)]
        [Kestrun.Forms.KrFormPartRule[]] $Rules,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter()]
        [string] $Description,

        [Parameter()]
        [switch] $Required,

        [Parameter()]
        [switch] $AllowOnlyOne,

        [Parameter()]
        [string[]] $AllowedContentTypes,

        [Parameter()]
        [string[]] $AllowedExtensions,

        [Parameter()]
        [long] $MaxBytes,

        [Parameter()]
        [Kestrun.Forms.KrPartDecodeMode] $DecodeMode = [Kestrun.Forms.KrPartDecodeMode]::None,

        [Parameter()]
        [string] $DestinationPath,

        [Parameter()]
        [switch] $StoreToDisk
    )
    begin {
        $bag = [System.Collections.Generic.List[Kestrun.Forms.KrFormPartRule]]::new()
    }
    process {
        if ( $null -ne $Rules ) {
            $bag.AddRange($Rules)
        }
    }
    end {
        $Rule = [Kestrun.Forms.KrFormPartRule]::new()
        $Rule.Name = $Name
        if ($PSBoundParameters.ContainsKey('Description')) {
            $Rule.Description = $Description
        }
        if ($PSBoundParameters.ContainsKey('Required')) {
            $Rule.Required = $Required.IsPresent
        }
        if ($PSBoundParameters.ContainsKey('AllowOnlyOne')) {
            $Rule.AllowMultiple = -not $AllowOnlyOne.IsPresent
        }
        if ($PSBoundParameters.ContainsKey('AllowedContentTypes')) {
            $Rule.AllowedContentTypes.Clear()
            foreach ($type in $AllowedContentTypes) {
                $Rule.AllowedContentTypes.Add($type)
            }
        }
        if ($PSBoundParameters.ContainsKey('AllowedExtensions')) {
            $Rule.AllowedExtensions.Clear()
            foreach ($ext in $AllowedExtensions) {
                $Rule.AllowedExtensions.Add($ext)
            }
        }
        if ($PSBoundParameters.ContainsKey('MaxBytes')) {
            $Rule.MaxBytes = $MaxBytes
        }
        if ($PSBoundParameters.ContainsKey('DecodeMode')) {
            $Rule.DecodeMode = $DecodeMode
        }
        if ($PSBoundParameters.ContainsKey('DestinationPath')) {
            $Rule.DestinationPath = $DestinationPath
        }
        if ($PSBoundParameters.ContainsKey('StoreToDisk')) {
            $Rule.StoreToDisk = $StoreToDisk.IsPresent
        }
        $bag.Add($Rule)
        , [Kestrun.Forms.KrFormPartRule[]] $bag.ToArray()
    }
}
