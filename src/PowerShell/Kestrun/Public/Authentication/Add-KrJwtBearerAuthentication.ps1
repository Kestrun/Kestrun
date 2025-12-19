<#
    .SYNOPSIS
        Adds JWT Bearer authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use JWT Bearer authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    .PARAMETER AuthenticationScheme
        The name of the authentication scheme.
        This name is used to identify the authentication scheme in the Kestrun server configuration.
    .PARAMETER DisplayName
        The display name for the authentication scheme.
        This name is shown in user interfaces and documentation.
    .PARAMETER DocId
        The documentation IDs to associate with this authentication scheme in OpenAPI documentation.
    .PARAMETER Description
        A description of the JWT Bearer authentication scheme.
    .PARAMETER Deprecated
        If specified, marks the authentication scheme as deprecated in OpenAPI documentation.
    .PARAMETER Options
        An instance of Kestrun.Authentication.JwtAuthOptions containing the JWT Bearer authentication configuration.
        This parameter is mandatory when using the 'Options' parameter set.
    .PARAMETER ValidationParameter
        The token validation parameters used to validate incoming JWT tokens.
        This parameter is mandatory when using the 'ValParamOption' parameter set.
    .PARAMETER ClaimPolicy
        The claim policy configuration for the authentication scheme.
    .PARAMETER ValidIssuer
        The valid issuer for the JWT tokens.
        This parameter is used to validate the issuer of incoming tokens.
    .PARAMETER ValidIssuers
        An array of valid issuers for the JWT tokens.
        This parameter is used to validate the issuer of incoming tokens.
    .PARAMETER ValidAudience
        The valid audience for the JWT tokens.
        This parameter is used to validate the audience of incoming tokens.
    .PARAMETER ValidAudiences
        An array of valid audiences for the JWT tokens.
        This parameter is used to validate the audience of incoming tokens.
    .PARAMETER ValidAlgorithms
        An array of valid algorithms for the JWT tokens.
        This parameter is used to validate the algorithm of incoming tokens.
    .PARAMETER SkipValidateIssuer
        A switch parameter that, when specified, skips validation of the issuer.
    .PARAMETER SkipValidateAudience
        A switch parameter that, when specified, skips validation of the audience.
    .PARAMETER SkipValidateLifetime
        A switch parameter that, when specified, skips validation of the token lifetime.
    .PARAMETER ValidateIssuerSigningKey
        A switch parameter that, when specified, validates the issuer signing key.
    .PARAMETER DoesNotRequireSignedTokens
        A switch parameter that, when specified, indicates that signed tokens are not required.
    .PARAMETER IssuerSigningKey
        The security key used to validate the issuer signing key.
    .PARAMETER IssuerSigningKeys
        An array of security keys used to validate the issuer signing key.
    .PARAMETER ClockSkew
        The amount of time the token validation should allow for clock skew.
    .PARAMETER DoesNotRequireExpirationTime
        A switch parameter that, when specified, indicates that expiration time validation is not required.
    .PARAMETER MapInboundClaims
        A switch parameter that, when specified, maps inbound claims to Microsoft identity model claims.
    .PARAMETER SaveToken
        A switch parameter that, when specified, saves the token in the authentication properties after a successful authentication.
    .PARAMETER PassThru
        A switch parameter that, when specified, returns the Kestrun server instance.
    .EXAMPLE
        Add-KrJWTBearerAuthentication -Server $server -Name "MyAuth" -ValidationParameter $validationParameter -ClaimPolicy $claimPolicy
        Configure Kestrun server to use JWT Bearer authentication with the specified validation parameters and claim policy.
    .EXAMPLE
        Add-KrJWTBearerAuthentication -Server $server -Name "MyAuth" -ValidIssuer "https://issuer" -ValidAudience "api" -ValidAlgorithms @("HS256") -SkipValidateIssuer -PassThru
        Configure Kestrun server to use JWT Bearer authentication with the specified issuer, audience, and algorithms, skipping issuer validation, and return the server instance.
    .EXAMPLE
        Add-KrJWTBearerAuthentication -Server $server -Name "MyAuth" -ValidIssuer "https://issuer" -ValidAudience "api" -ValidAlgorithms @("HS256") -SkipValidateIssuer -PassThru
        Configure Kestrun server to use JWT Bearer authentication with the specified issuer, audience, and algorithms, skipping issuer validation, and return the server instance.
    .EXAMPLE
        Add-KrJWTBearerAuthentication -Server $server -Name "MyAuth" -ValidIssuer "https://issuer" -ValidAudience "api" -ValidAlgorithms @("HS256") -SkipValidateIssuer -PassThru
        Configure Kestrun server to use JWT Bearer authentication with the specified issuer, audience, and algorithms, skipping issuer validation, and return the server instance.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbearerauthenticationextensions.addjwtbearerauthentication?view=aspnetcore-8.0
    .NOTES
        This function is part of the Kestrun.Authentication module and is used to configure JWT Bearer authentication for Kestrun servers.
        Maps to Kestrun.Hosting.KestrunHostAuthnExtensions.AddJwtBearerAuthentication
#>
function Add-KrJWTBearerAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$AuthenticationScheme = [Kestrun.Authentication.AuthenticationDefaults]::JwtBearerAuthenticationSchemeName,

        [Parameter()]
        [string]$DisplayName = [Kestrun.Authentication.AuthenticationDefaults]::JwtBearerDisplayName,

        [Parameter()]
        [string[]]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultDocumentationIds,

        [Parameter(ParameterSetName = 'Items')]
        [string] $Description,

        [Parameter(ParameterSetName = 'Items')]
        [switch] $Deprecated,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Authentication.JwtAuthOptions]$Options,

        [Parameter(Mandatory = $true, ParameterSetName = 'ValParamOption')]
        [Microsoft.IdentityModel.Tokens.TokenValidationParameters]$ValidationParameter,

        [Parameter()]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicy,

        [Parameter(ParameterSetName = 'Items')]
        [string] $ValidIssuer,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$ValidIssuers,
        [Parameter(ParameterSetName = 'Items')]
        [string] $ValidAudience,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$ValidAudiences,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$ValidAlgorithms,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $SkipValidateIssuer ,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $SkipValidateAudience ,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $SkipValidateLifetime ,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $ValidateIssuerSigningKey,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $DoesNotRequireExpirationTime ,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $DoesNotRequireSignedTokens,
        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.IdentityModel.Tokens.SecurityKey]$IssuerSigningKey,
        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.IdentityModel.Tokens.SecurityKey[]]$IssuerSigningKeys,
        [Parameter(ParameterSetName = 'Items')]
        [TimeSpan]$ClockSkew,
        [Parameter(ParameterSetName = 'Items')]
        [Parameter(ParameterSetName = 'ValParamOption')]
        [switch]$MapInboundClaims,
        [Parameter(ParameterSetName = 'Items')]
        [Parameter(ParameterSetName = 'ValParamOption')]
        [switch]$SaveToken,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        # Build Options only when not provided directly
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Authentication.JwtAuthOptions]::new()

            # Build ValidationParameter only when not provided directly
            if ($PSCmdlet.ParameterSetName -ne 'ValParamOption') {
                $ValidationParameter = [Microsoft.IdentityModel.Tokens.TokenValidationParameters]::new()
                if ($PSBoundParameters.ContainsKey('ValidIssuer')) { $ValidationParameter.ValidIssuer = $ValidIssuer }
                if ($PSBoundParameters.ContainsKey('ValidIssuers')) { $ValidationParameter.ValidIssuers = $ValidIssuers }
                if ($PSBoundParameters.ContainsKey('ValidAudience')) { $ValidationParameter.ValidAudience = $ValidAudience }
                if ($PSBoundParameters.ContainsKey('ValidAudiences')) { $ValidationParameter.ValidAudiences = $ValidAudiences }
                if ($PSBoundParameters.ContainsKey('ValidAlgorithms')) { $ValidationParameter.ValidAlgorithms = $ValidAlgorithms }
                if ($PSBoundParameters.ContainsKey('SkipValidateIssuer')) { $ValidationParameter.ValidateIssuer = -not $SkipValidateIssuer.IsPresent }
                if ($PSBoundParameters.ContainsKey('SkipValidateAudience')) { $ValidationParameter.ValidateAudience = -not $SkipValidateAudience.IsPresent }
                if ($PSBoundParameters.ContainsKey('SkipValidateLifetime')) { $ValidationParameter.ValidateLifetime = -not $SkipValidateLifetime.IsPresent }
                if ($PSBoundParameters.ContainsKey('ValidateIssuerSigningKey')) { $ValidationParameter.ValidateIssuerSigningKey = $ValidateIssuerSigningKey.IsPresent }

                if ($PSBoundParameters.ContainsKey('RequireExpirationTime')) { $ValidationParameter.RequireExpirationTime = -not $DoesNotRequireExpirationTime.IsPresent }
                if ($PSBoundParameters.ContainsKey('RequireSignedTokens')) { $ValidationParameter.RequireSignedTokens = -not$DoesNotRequireSignedTokens.IsPresent }

                if ($PSBoundParameters.ContainsKey('IssuerSigningKey')) { $ValidationParameter.IssuerSigningKey = $IssuerSigningKey }
                if ($PSBoundParameters.ContainsKey('IssuerSigningKeys')) { $ValidationParameter.IssuerSigningKeys = $IssuerSigningKeys }

                if ($PSBoundParameters.ContainsKey('ClockSkew')) { $ValidationParameter.ClockSkew = $ClockSkew }

                # Set description
                if (-not ([string]::IsNullOrWhiteSpace($Description))) { $Options.Description = $Description }

                # Set the Deprecated option
                $Options.Deprecated = $Deprecated.IsPresent

                # Map inbound claims
                $ValidationParameter.MapInboundClaims = $MapInboundClaims.IsPresent
                # Save token
                $Options.SaveToken = $SaveToken.IsPresent
            }

            $Options.TokenValidationParameters = $ValidationParameter
            # OpenAPI documentation IDs
            $Options.DocumentationId = $DocId
            # Claim policy
            $Options.ClaimPolicy = $ClaimPolicy
        }
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddJwtBearerAuthentication(
            $Server, $AuthenticationScheme, $DisplayName, $Options ) | Out-Null
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
