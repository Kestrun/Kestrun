<#
    .SYNOPSIS
        Adds API key authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use API key authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    .PARAMETER Name
        The name of the API key authentication scheme.
    .PARAMETER Options
        The options to configure the API key authentication.
    .PARAMETER ScriptBlock
        A script block that contains the logic for validating the API key.
    .PARAMETER Code
        C# or VBNet code that contains the logic for validating the API key.
    .PARAMETER CodeLanguage
        The scripting language of the code used for validating the API key.
    .PARAMETER CodeFilePath
        Path to a file containing C# code that contains the logic for validating the API key.
    .PARAMETER ExpectedKey
        The expected API key to validate against.
    .PARAMETER HeaderName
        The name of the header to look for the API key.
    .PARAMETER AdditionalHeaderNames
        Additional headers to check for the API key.
    .PARAMETER AllowQueryStringFallback
        If specified, allows the API key to be provided in the query string.
    .PARAMETER AllowInsecureHttp
        If specified, allows the API key to be provided over HTTP instead of HTTPS.
    .PARAMETER EmitChallengeHeader
        If specified, emits a challenge header when the API key is missing or invalid.
    .PARAMETER ChallengeHeaderFormat
        The format of the challenge header to emit.
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
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -ExpectedKey '12345' -HeaderName 'X-Api-Key'
        This example adds API key authentication to the server with the specified expected key and header name.
    .EXAMPLE
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -ScriptBlock {
            param($username, $password)
            return $username -eq 'admin' -and $password -eq 'password'
        }
        This example adds API key authentication using a script block to validate the API key.
    .EXAMPLE
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -Code @"
            return username == "admin" && password == "password";
        "@
        This example adds API key authentication using C# code to validate the API key.
    .EXAMPLE
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -CodeFilePath 'C:\path\to\code.cs'
        This example adds API key authentication using a C# code file to validate the API key.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.apikey.apikeyauthenticationoptions?view=aspnetcore-8.0
    .NOTES
        This cmdlet is used to configure API key authentication for the Kestrun server, allowing you to secure your APIs with API keys.
#>
function Add-KrApiKeyAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'ItemsScriptBlock')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Authentication.ApiKeyAuthenticationOptions]$Options,

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

        [Parameter(Mandatory = $true, ParameterSetName = 'v4')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v4_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v4_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v4_i3')]
        [string]$ExpectedKey,

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
        [Parameter(ParameterSetName = 'v4')]
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
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
        [Parameter(ParameterSetName = 'v4')]
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
        [string[]]$AdditionalHeaderNames,

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
        [Parameter(ParameterSetName = 'v4')]
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
        [switch]$AllowQueryStringFallback,

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
        [Parameter(ParameterSetName = 'v4')]
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
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
        [Parameter(ParameterSetName = 'v4')]
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
        [switch]$EmitChallengeHeader,

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
        [Parameter(ParameterSetName = 'v4')]
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
        [Kestrun.Authentication.ApiKeyChallengeFormat]$ChallengeHeaderFormat,

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
        [Parameter(ParameterSetName = 'v4')]
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
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
        [Parameter(ParameterSetName = 'v4_i1')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Parameter(ParameterSetName = 'v4_i3')]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicyConfig,

        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i1')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v4_i1')]
        [scriptblock]$IssueClaimsScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i2')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v4_i2')]
        [string]$IssueClaimsCode,

        [Parameter(ParameterSetName = 'v3_i2')]
        [Parameter(ParameterSetName = 'v2_i2')]
        [Parameter(ParameterSetName = 'v1_i2')]
        [Parameter(ParameterSetName = 'v4_i2')]
        [Kestrun.Scripting.ScriptLanguage]$IssueClaimsCodeLanguage = [Kestrun.Scripting.ScriptLanguage]::CSharp,

        [Parameter(Mandatory = $true, ParameterSetName = 'v3_i3')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v2_i3')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v1_i3')]
        [Parameter(Mandatory = $true, ParameterSetName = 'v4_i3')]
        [string]$IssueClaimsCodeFilePath,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Authentication.ApiKeyAuthenticationOptions]::new()
            $Options.ValidateCodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()
            if (-not [string]::IsNullOrWhiteSpace($ExpectedKey)) {
                $Options.ExpectedKey = $ExpectedKey
            } elseif ($null -ne $ScriptBlock) {
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
            if ($AdditionalHeaderNames.Count -gt 0) {
                $Options.AdditionalHeaderNames = $AdditionalHeaderNames
            }
            if ($AllowQueryStringFallback.IsPresent) {
                $Options.AllowQueryStringFallback = $AllowQueryStringFallback.IsPresent
            }
            if ($EmitChallengeHeader.IsPresent) {
                $Options.EmitChallengeHeader = $EmitChallengeHeader.IsPresent
            }
            if ($null -ne $ChallengeHeaderFormat) {
                $Options.ChallengeHeaderFormat = $ChallengeHeaderFormat
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

        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddApiKeyAuthentication(
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

