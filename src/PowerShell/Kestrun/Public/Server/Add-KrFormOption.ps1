<#
.SYNOPSIS
    Adds a form option to the Kestrun server.
.DESCRIPTION
    This function creates and adds a form option to the Kestrun server's form options collection.
    It allows you to specify various parameters for the form option, such as name, description,
    allowed content types, upload path, and part rules.
.PARAMETER Name
    The name of the form option.
    If not provided, a unique name will be generated as "FormOption_{GUID}". Also used as the key
    when registering the form option in the server's form options collection.
    If not unique, the registration will fail and $null will be returned.
    If not provided, the form option will still be created and returned, but not registered.
.PARAMETER Description
    A description of the form option.
.PARAMETER AllowUnknownRequestContentType
    Indicates whether to allow unknown request content types.
.PARAMETER AllowedRequestContentTypes
    An array of allowed request content types for the form option.
.PARAMETER DefaultUploadPath
    The default upload path for files.
.PARAMETER ComputeSha256
    Indicates whether to compute the SHA-256 hash of uploaded files.
.PARAMETER EnablePartDecompression
    Indicates whether to enable decompression of parts.
.PARAMETER AllowedPartContentEncodings
    An array of allowed content encodings for parts.
.PARAMETER MaxDecompressedBytesPerPart
    The maximum number of decompressed bytes per part.
.PARAMETER RejectUnknownContentEncoding
    Indicates whether to reject unknown content encodings.
.PARAMETER PartRules
    An array of form part rules associated with the form option.
.PARAMETER Logger
    The logger to use for logging.
.PARAMETER MaxRequestBodyBytes
    The maximum size in bytes for the request body.
.PARAMETER MaxPartBodyBytes
    The maximum size in bytes for each part body.
.PARAMETER MaxParts
    The maximum number of parts allowed in the form.
.PARAMETER MaxHeaderBytesPerPart
    The maximum size in bytes for headers per part.
.PARAMETER MaxFieldValueBytes
    The maximum size in bytes for field values.
.PARAMETER MaxNestingDepth
    The maximum nesting depth for multipart forms.
.PARAMETER PassThru
    If specified, the cmdlet will return the created form option.
    If a Name is not provided, the created form option will always be returned.
.EXAMPLE
    Add-KrFormOption -Name 'fileUpload' -DefaultUploadPath 'C:\Uploads' -ComputeSha256 -PartRules $rules
    This example adds a form option named 'fileUpload' with a default upload path of 'C:\Uploads',
    enables SHA-256 computation, and associates the specified part rules.
.EXAMPLE
    New-KrFormPartRule -Name 'file' -Required -AllowedContentTypes 'text/plain'|
        Add-KrFormOption -Name 'textFileUpload' -PassThru
    This example creates a form part rule for a required text file and adds it to a new form option
    named 'textFileUpload'.
.NOTES
    This function is part of the Kestrun.Forms module and is used to define form options.
#>
function Add-KrFormOption {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Forms.KrFormOptions])]
    param(
        [Parameter()]
        [string] $Name,

        [Parameter()]
        [string] $Description,

        [Parameter()]
        [switch] $AllowUnknownRequestContentType,

        [Parameter()]
        [string[]] $AllowedRequestContentTypes = @('multipart/form-data'),

        [Parameter()]
        [string] $DefaultUploadPath,

        [Parameter()]
        [switch] $ComputeSha256,

        [Parameter()]
        [switch] $EnablePartDecompression,

        [Parameter()]
        [string[]] $AllowedPartContentEncodings,

        [Parameter()]
        [long] $MaxDecompressedBytesPerPart,

        [Parameter()]
        [switch] $RejectUnknownContentEncoding,

        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Forms.KrFormPartRule[]] $PartRules,

        [Parameter()]
        [Serilog.ILogger] $Logger,

        # Limits (optional)
        [Parameter()]
        [long] $MaxRequestBodyBytes,

        [Parameter()]
        [long] $MaxPartBodyBytes ,

        [Parameter()]
        [int] $MaxParts,

        [Parameter()]
        [int] $MaxHeaderBytesPerPart,

        [Parameter()]
        [long] $MaxFieldValueBytes,

        [Parameter()]
        [int] $MaxNestingDepth,

        [Parameter()]
        [switch] $PassThru

    )
    begin {
        $Server = Resolve-KestrunServer
        if (-not $Server) { throw 'Server is not initialized. Call New-KrServer and Enable-KrConfiguration first.' }
    }
    process {
        $Options = [Kestrun.Forms.KrFormOptions]::new()

        if ($PSBoundParameters.ContainsKey('Description')) {
            $Options.Description = $Description
        }
        if ($PSBoundParameters.ContainsKey('AllowedRequestContentTypes')) {
            $Options.AllowedRequestContentTypes.Clear()
            $Options.AllowedRequestContentTypes.AddRange($AllowedRequestContentTypes)
        }
        if ($PSBoundParameters.ContainsKey('AllowUnknownRequestContentType')) {
            $Options.RejectUnknownRequestContentType = -not $AllowUnknownRequestContentType.IsPresent
        }
        if ($PSBoundParameters.ContainsKey('DefaultUploadPath')) {
            $Options.DefaultUploadPath = $DefaultUploadPath
        }
        if ($PSBoundParameters.ContainsKey('ComputeSha256')) {
            $Options.ComputeSha256 = $ComputeSha256.IsPresent
        }
        if ($PSBoundParameters.ContainsKey('EnablePartDecompression')) {
            $Options.EnablePartDecompression = $EnablePartDecompression.IsPresent
        }
        if ($PSBoundParameters.ContainsKey('AllowedPartContentEncodings')) {
            $Options.AllowedPartContentEncodings.Clear()
            $Options.AllowedPartContentEncodings.AddRange($AllowedPartContentEncodings)
        }
        if ($PSBoundParameters.ContainsKey('MaxDecompressedBytesPerPart')) {
            $Options.MaxDecompressedBytesPerPart = $MaxDecompressedBytesPerPart
        }
        if ($PSBoundParameters.ContainsKey('RejectUnknownContentEncoding')) {
            $Options.RejectUnknownContentEncoding = $RejectUnknownContentEncoding.IsPresent
        }
        if ($PSBoundParameters.ContainsKey('PartRules')) {
            $Options.Rules.Clear()
            foreach ($rule in $PartRules) {
                $Options.Rules.Add($rule)
            }
        }
        if ($PSBoundParameters.ContainsKey('Logger')) {
            $Options.Logger = $Logger
        } else {
            $Options.Logger = $Server.Logger
        }

        # Limits
        if ($PSBoundParameters.ContainsKey('MaxRequestBodyBytes')) {
            $Options.MaxRequestBodyBytes = $MaxRequestBodyBytes
        }
        if ($PSBoundParameters.ContainsKey('MaxPartBodyBytes')) {
            $Options.MaxPartBodyBytes = $MaxPartBodyBytes
        }
        if ($PSBoundParameters.ContainsKey('MaxParts')) {
            $Options.MaxParts = $MaxParts
        }
        if ($PSBoundParameters.ContainsKey('MaxHeaderBytesPerPart')) {
            $Options.MaxHeaderBytesPerPart = $MaxHeaderBytesPerPart
        }
        if ($PSBoundParameters.ContainsKey('MaxFieldValueBytes')) {
            $Options.MaxFieldValueBytes = $MaxFieldValueBytes
        }
        if ($PSBoundParameters.ContainsKey('MaxNestingDepth')) {
            $Options.MaxNestingDepth = $MaxNestingDepth
        }

        # Register the option
        if ($PSBoundParameters.ContainsKey('Name')) {
            $Options.Name = $Name
            # Register the option in the server's form options collection
            if (-not $Server.AddFormOption($Options)) {
                return $null
            }
            # Return the created options if PassThru is specified
            if (-not $PassThru.IsPresent) {
                return
            }
        } else {
            # Generate a unique name if not provided
            $Options.Name = "FormOption_$([System.Guid]::NewGuid().ToString())"
        }
        return $Options
    }
}
