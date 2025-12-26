<#
    .SYNOPSIS
        Adds API key authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use API key authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    .PARAMETER AuthenticationScheme
        The name of the API key authentication scheme.
    .PARAMETER DisplayName
        The display name of the API key authentication scheme.
    .PARAMETER Description
        A description of the API key authentication scheme.
    .PARAMETER Deprecated
        If specified, marks the authentication scheme as deprecated in OpenAPI documentation.
    .PARAMETER DocId
        The documentation IDs to associate with this authentication scheme in OpenAPI documentation.
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
    .PARAMETER StaticApiKey
        The expected API key to validate against.
    .PARAMETER ApiKeyName
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
        Add-KrApiKeyAuthentication -AuthenticationScheme 'MyApiKey' -StaticApiKey '12345' -ApiKeyName 'X-Api-Key'
        This example adds API key authentication to the server with the specified expected key and header name.
    .EXAMPLE
        Add-KrApiKeyAuthentication -AuthenticationScheme 'MyApiKey' -ScriptBlock {
            param($username, $password)
            return $username -eq 'admin' -and $password -eq 'password'
        }
        This example adds API key authentication using a script block to validate the API key.
    .EXAMPLE
        Add-KrApiKeyAuthentication -AuthenticationScheme 'MyApiKey' -Code @"
            return username == "admin" && password == "password";
        "@
        This example adds API key authentication using C# code to validate the API key.
    .EXAMPLE
        Add-KrApiKeyAuthentication -AuthenticationScheme 'MyApiKey' -CodeFilePath 'C:\path\to\code.cs'
        This example adds API key authentication using a C# code file to validate the API key.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.apikey.apikeyauthenticationoptions?view=aspnetcore-8.0
    .NOTES
        This cmdlet is used to configure API key authentication for the Kestrun server, allowing you to secure your APIs with API keys.
#>
function Add-KrApiKeyAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'ScriptBlock')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$AuthenticationScheme = [Kestrun.Authentication.AuthenticationDefaults]::ApiKeySchemeName,

        [Parameter()]
        [string]$DisplayName = [Kestrun.Authentication.AuthenticationDefaults]::ApiKeyDisplayName,

        [Parameter(Mandatory = $false, ParameterSetName = 'ScriptBlock')]
        [Parameter(Mandatory = $false, ParameterSetName = 'CodeInline')]
        [Parameter(Mandatory = $false, ParameterSetName = 'StaticKey')]
        [Parameter(Mandatory = $false, ParameterSetName = 'CodeFile')]
        [string]$Description,

        [Parameter(Mandatory = $false, ParameterSetName = 'ScriptBlock')]
        [Parameter(Mandatory = $false, ParameterSetName = 'CodeInline')]
        [Parameter(Mandatory = $false, ParameterSetName = 'StaticKey')]
        [Parameter(Mandatory = $false, ParameterSetName = 'CodeFile')]
        [switch]$Deprecated,

        [Parameter()]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        # 1. Direct options
        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Authentication.ApiKeyAuthenticationOptions]$Options,

        # 2. Validation via ScriptBlock
        [Parameter(Mandatory = $true, ParameterSetName = 'ScriptBlock')]
        [scriptblock]$ScriptBlock,

        # 3. Validation via inline code (C#/VB)
        [Parameter(Mandatory = $true, ParameterSetName = 'CodeInline')]
        [string]$Code,

        [Parameter(ParameterSetName = 'CodeInline')]
        [Kestrun.Scripting.ScriptLanguage]$CodeLanguage = [Kestrun.Scripting.ScriptLanguage]::CSharp,

        # 4. Validation via code file
        [Parameter(Mandatory = $true, ParameterSetName = 'CodeFile')]
        [string]$CodeFilePath,

        # 5. Validation via static API key
        [Parameter(Mandatory = $true, ParameterSetName = 'StaticKey')]
        [string]$StaticApiKey,

        [Parameter()]
        # Common API key config (all parameter sets)
        [Microsoft.OpenApi.ParameterLocation]$In = [Microsoft.OpenApi.ParameterLocation]::Header,
        [Parameter()]
        [string]$ApiKeyName,
        [Parameter()]
        [string[]]$AdditionalHeaderNames,
        [Parameter()]
        [switch]$AllowQueryStringFallback,
        [Parameter()]
        [switch]$AllowInsecureHttp,
        [Parameter()]
        [switch]$EmitChallengeHeader,
        [Parameter()]
        [Kestrun.Authentication.ApiKeyChallengeFormat]$ChallengeHeaderFormat,

        [Parameter()]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicyConfig,

        # Optional "issue claims" configuration (independent from validation mode)
        [scriptblock]$IssueClaimsScriptBlock,
        [string]$IssueClaimsCode,
        [Kestrun.Scripting.ScriptLanguage]$IssueClaimsCodeLanguage = [Kestrun.Scripting.ScriptLanguage]::CSharp,
        [string]$IssueClaimsCodeFilePath,

        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Build Options only when not provided directly
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Authentication.ApiKeyAuthenticationOptions]::new()
            # Set host reference
            $Options.Host = $Server
            $Options.ValidateCodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()

            switch ($PSCmdlet.ParameterSetName) {
                'ScriptBlock' {
                    $Options.ValidateCodeSettings.Code = $ScriptBlock.ToString()
                    $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                }
                'CodeInline' {
                    $Options.ValidateCodeSettings.Code = $Code
                    $Options.ValidateCodeSettings.Language = $CodeLanguage
                }
                'CodeFile' {
                    if (-not (Test-Path -Path $CodeFilePath)) {
                        throw "The specified code file path does not exist: $CodeFilePath"
                    }

                    $extension = [System.IO.Path]::GetExtension($CodeFilePath)

                    switch ($extension.ToLowerInvariant()) {
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
                            throw "Unsupported '$extension' code file extension for validation."
                        }
                    }

                    $Options.ValidateCodeSettings.Code = Get-Content -Path $CodeFilePath -Raw
                }
                'StaticKey' {
                    $Options.StaticApiKey = $StaticApiKey
                }
            }

            # Common API key options
            if ($PSBoundParameters.ContainsKey('ApiKeyName')) {
                $Options.ApiKeyName = $ApiKeyName
            }

            # Set location of API key
            $Options.In = $In

            if ($PSBoundParameters.ContainsKey('AdditionalHeaderNames') -and $AdditionalHeaderNames.Count -gt 0) {
                $Options.AdditionalHeaderNames = $AdditionalHeaderNames
            }

            if ($AllowQueryStringFallback.IsPresent) {
                $Options.AllowQueryStringFallback = $true
            }

            $Options.AllowInsecureHttp = $AllowInsecureHttp.IsPresent

            $Options.EmitChallengeHeader = $EmitChallengeHeader.IsPresent

            $Options.Deprecated = $Deprecated.IsPresent

            if ($PSBoundParameters.ContainsKey('ChallengeHeaderFormat')) {
                $Options.ChallengeHeaderFormat = $ChallengeHeaderFormat
            }

            if ($PSBoundParameters.ContainsKey('ClaimPolicyConfig')) {
                $Options.ClaimPolicyConfig = $ClaimPolicyConfig
            }

            if (-not ([string]::IsNullOrWhiteSpace($Description))) {
                $Options.Description = $Description
            }

            # Optional issue-claims settings (single-choice)
            $issueModes = @()
            if ($PSBoundParameters.ContainsKey('IssueClaimsScriptBlock')) { $issueModes += 'ScriptBlock' }
            if ($PSBoundParameters.ContainsKey('IssueClaimsCode')) { $issueModes += 'Code' }
            if ($PSBoundParameters.ContainsKey('IssueClaimsCodeFilePath')) { $issueModes += 'File' }

            if ($issueModes.Count -gt 1) {
                throw 'Specify only one of -IssueClaimsScriptBlock, -IssueClaimsCode, or -IssueClaimsCodeFilePath.'
            }

            if ($issueModes.Count -eq 1) {
                $Options.IssueClaimsCodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()

                switch ($issueModes[0]) {
                    'ScriptBlock' {
                        $Options.IssueClaimsCodeSettings.Code = $IssueClaimsScriptBlock.ToString()
                        $Options.IssueClaimsCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                    }
                    'Code' {
                        $Options.IssueClaimsCodeSettings.Code = $IssueClaimsCode
                        $Options.IssueClaimsCodeSettings.Language = $IssueClaimsCodeLanguage
                    }
                    'File' {
                        if (-not (Test-Path -Path $IssueClaimsCodeFilePath)) {
                            throw "The specified issue-claims code file path does not exist: $IssueClaimsCodeFilePath"
                        }

                        $issueExt = [System.IO.Path]::GetExtension($IssueClaimsCodeFilePath)

                        switch ($issueExt.ToLowerInvariant()) {
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
                                throw "Unsupported '$issueExt' code file extension for issue-claims."
                            }
                        }

                        $Options.IssueClaimsCodeSettings.Code = Get-Content -Path $IssueClaimsCodeFilePath -Raw
                    }
                }
            }

            # OpenAPI documentation IDs
            $Options.DocumentationId = $DocId
        }

        # Add API key authentication to the server
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddApiKeyAuthentication(
            $Server, $AuthenticationScheme, $DisplayName, $Options ) | Out-Null

        # Return the modified server instance if PassThru is specified
        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
