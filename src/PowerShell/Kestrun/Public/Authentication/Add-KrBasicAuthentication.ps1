<#
    .SYNOPSIS
        Adds basic authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use basic authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    .PARAMETER Name
        The name of the basic authentication scheme.
    .PARAMETER Options
        The options to configure the basic authentication.
    .PARAMETER ScriptBlock
        A script block that contains the logic for validating the username and password.
    .PARAMETER Code
        C# or VBNet code that contains the logic for validating the username and password.
    .PARAMETER CodeLanguage
        The scripting language of the code used for validating the username and password.
    .PARAMETER CodeFilePath
        Path to a file containing C# code that contains the logic for validating the username and password.
    .PARAMETER HeaderName
        The name of the header to look for the basic authentication credentials.
    .PARAMETER Base64Encoded
        If specified, the credentials are expected to be Base64 encoded.
    .PARAMETER SuppressWwwAuthenticate
        If specified, the server will not emit the WWW-Authenticate header in responses.
    .PARAMETER SeparatorRegex
        A regular expression to use for separating multiple credentials in the header.
    .PARAMETER Realm
        The realm for the basic authentication.
    .PARAMETER AllowInsecureHttp
        If specified, allows the basic authentication to be used over HTTP instead of HTTPS.
    .PARAMETER Logger
        A logger to use for logging authentication events.
    .PARAMETER ClaimPolicyConfig
        Configuration for claim policies to apply during authentication.
    .PARAMETER IssueClaimsScriptBlock
        A script block that contains the logic for issuing claims after successful authentication.
    .PARAMETER IssueClaimsCode
        C# or VBNet code that contains the logic for issuing claims after successful authentication.
    .PARAMETER IssueClaimsCodeLanguage
        The scripting language of the code used for issuing claims.
    .PARAMETER IssueClaimsCodeFilePath
        Path to a file containing the code that contains the logic for issuing claims after successful authentication
    .PARAMETER PassThru
        If specified, returns the modified server instance after adding the authentication.
    .EXAMPLE
        Add-KrBasicAuthentication -Server $server -Name "MyAuth" -Options $options -ScriptBlock $scriptBlock
        Configure Kestrun server to use basic authentication with the specified script block.
    .EXAMPLE
        Add-KrBasicAuthentication -Server $server -Name "MyAuth" -Options $options -Code $code -CodeLanguage $codeLanguage
        Configure Kestrun server to use basic authentication with the specified code.
    .EXAMPLE
        Add-KrBasicAuthentication -Server $server -Name "MyAuth" -Options $options -CodeFilePath $codeFilePath
        Configure Kestrun server to use basic authentication with the specified code file.
    .NOTES
        This function is part of the Kestrun.Authentication module and is used to configure basic authentication for Kestrun servers.
        Maps to Kestrun.Hosting.KestrunHostAuthExtensions.AddBasicAuthentication
#>
function Add-KrBasicAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'v1')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Authentication.BasicAuthenticationOptions]$Options,

        [Parameter(Mandatory = $true, ParameterSetName = 'v1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i3')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'v2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i3')]
        [string]$Code,

        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Kestrun.Scripting.ScriptLanguage]$CodeLanguage = [Kestrun.Scripting.ScriptLanguage]::CSharp,

        [Parameter(Mandatory = $true, ParameterSetName = 'v3')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i3')]
        [string]$CodeFilePath,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [string]$HeaderName,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [switch]$Base64Encoded,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [switch]$SuppressWwwAuthenticate,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [Regex]$SeparatorRegex,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [string]$Realm,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [switch]$AllowInsecureHttp,

        [Parameter(ParameterSetName = 'v1')]
        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [Serilog.ILogger]$Logger,

        [Parameter(ParameterSetName = 'v1_i1')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v1_i3')]
        [Parameter(ParameterSetName = 'v2_i1')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v2_i3')]
        [Parameter(ParameterSetName = 'v3_i1')]
        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v3_i3')]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicyConfig,

        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i1')]
        [scriptblock]$IssueClaimsScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i2')]
        [string]$IssueClaimsCode,

        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Kestrun.Scripting.ScriptLanguage]$IssueClaimsCodeLanguage = [Kestrun.Scripting.ScriptLanguage]::CSharp,

        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i3')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i3')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i3')]
        [string]$IssueClaimsCodeFilePath,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Authentication.BasicAuthenticationOptions]::new()
            $Options.ValidateCodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()
            if ($null -ne $ScriptBlock) {
                $Options.ValidateCodeSettings.Code = $ScriptBlock.ToString()
                $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
            } elseif (-not [string]::IsNullOrWhiteSpace($Code)) {
                $Options.ValidateCodeSettings.Code = $Code
                $Options.ValidateCodeSettings.Language = $CodeLanguage
            } elseif (-not [string]::IsNullOrWhiteSpace($CodeFilePath)) {
                if (-not (Test-Path -Path $CodeFilePath)) {
                    throw "The specified code file path does not exist: $CodeFilePath"
                }
                $extension = Split-Path -Path $CodeFilePath -Extension
                switch ($extension) {
                    '.ps1' {
                        $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                    }
                    '.cs' {
                        $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                    }
                    '.vb' {
                        $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::VisualBasic
                    }
                    default {
                        throw "Unsupported '$extension' code file extension."
                    }
                }
                $Options.ValidateCodeSettings.Code = Get-Content -Path $CodeFilePath -Raw
            }

            if (-not [string]::IsNullOrWhiteSpace($HeaderName)) {
                $Options.HeaderName = $HeaderName
            }
            if ($Base64Encoded.IsPresent) {
                $Options.Base64Encoded = $Base64Encoded.IsPresent
            }
            if ($SuppressWwwAuthenticate.IsPresent) {
                $Options.SuppressWwwAuthenticate = $SuppressWwwAuthenticate.IsPresent
            }
            if ($null -ne $SeparatorRegex) {
                $Options.SeparatorRegex = $SeparatorRegex
            }
            if (-not [string]::IsNullOrWhiteSpace($Realm)) {
                $Options.Realm = $Realm
            }
            if ($AllowInsecureHttp.IsPresent) {
                $Options.RequireHttps = $false
            } else {
                $Options.RequireHttps = $true
            }
            if ($null -ne $ClaimPolicyConfig) {
                $Options.ClaimPolicyConfig = $ClaimPolicyConfig
            }
            if ($null -ne $Logger) {
                $Options.Logger = $Logger
            }

            if ($PSCmdlet.ParameterSetName.contains('_')) {

                $Options.IssueClaimsCodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()
                if ($null -ne $IssueClaimsScriptBlock) {
                    $Options.IssueClaimsCodeSettings.Code = $IssueClaimsScriptBlock.ToString()
                    $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                } elseif (-not [string]::IsNullOrWhiteSpace($IssueClaimsCode)) {
                    $Options.IssueClaimsCodeSettings.Code = $IssueClaimsCode
                    $Options.IssueClaimsCodeSettings.Language = $IssueClaimsCodeLanguage
                } elseif (-not [string]::IsNullOrWhiteSpace($IssueClaimsCodeFilePath)) {
                    if (-not (Test-Path -Path $IssueClaimsCodeFilePath)) {
                        throw "The specified code file path does not exist: $IssueClaimsCodeFilePath"
                    }
                    $extension = Split-Path -Path $IssueClaimsCodeFilePath -Extension
                    switch ($extension) {
                        '.ps1' {
                            $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                        }
                        '.cs' {
                            $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                        }
                        '.vb' {
                            $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::VisualBasic
                        }
                        default {
                            throw "Unsupported '$extension' code file extension."
                        }
                    }
                    $Options.IssueClaimsCodeSettings.Code = Get-Content -Path $CodeFilePath -Raw
                }
            }
        }
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostAuthExtensions]::AddBasicAuthentication(
            $Server,
            $Name,
            $Options
        ) | Out-Null
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}
