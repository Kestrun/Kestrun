<#
    .SYNOPSIS
        Builds a Private Key JWT (client assertion) for OAuth2/OIDC client authentication.
    .DESCRIPTION
        This function generates a signed JWT suitable for private_key_jwt client authentication
        using either an X509 certificate or a JWK JSON string as the signing key.

        It uses Kestrun.Certificates.CertificateManager.BuildPrivateKeyJwt[FromJwkJson]
        under the hood, with:
          - iss = client_id
          - sub = client_id
          - aud = token endpoint
          - short lifetime (about 2 minutes) and a random jti.
    .PARAMETER Certificate
        The X509Certificate2 object whose private key will be used to sign the JWT.
    .PARAMETER Path
        Path to a certificate on disk. This is resolved via Resolve-KrPath and imported
        using [Kestrun.Certificates.CertificateManager]::Import().
    .PARAMETER JwkJson
        A JWK JSON string representing the key (typically an RSA private key JWK).
    .PARAMETER ClientId
        The client identifier; used as both issuer (iss) and subject (sub) in the JWT.
    .PARAMETER TokenEndpoint
        The token endpoint URL; used as the audience (aud) in the JWT.
    .PARAMETER Authority
        The authority (issuer) URL; used to discover the token endpoint via OIDC discovery.
    .PARAMETER WhatIf
        Shows what would happen if the command runs. The command is not run.
    .PARAMETER Confirm
        Prompts you for confirmation before running the command. The command is not run unless you respond
        affirmatively.
    .OUTPUTS
        [string] – the signed JWT (client assertion).
    .EXAMPLE
        $cert | New-KrPrivateKeyJwt -ClientId 'my-client' -TokenEndpoint 'https://idp.example.com/oauth2/token'

        Builds a private_key_jwt client assertion using the certificate from the pipeline.
    .EXAMPLE
        New-KrPrivateKeyJwt -Path './certs/client.pfx' -ClientId 'my-client' `
            -TokenEndpoint 'https://idp.example.com/oauth2/token'

        Imports the certificate from disk and generates a private_key_jwt.
    .EXAMPLE
        $jwk = ConvertTo-KrJwkJson -Certificate $cert -IncludePrivateParameters
        New-KrPrivateKeyJwt -JwkJson $jwk -ClientId 'my-client' `
            -TokenEndpoint 'https://idp.example.com/oauth2/token'

        Generates a private_key_jwt using a private RSA JWK JSON string.
    .EXAMPLE
        $cert | New-KrPrivateKeyJwt `
            -ClientId 'interactive.confidential' `
            -Authority 'https://demo.duendesoftware.com'

        Uses discovery to resolve the token_endpoint from the authority.
    .NOTES
        Requires the Kestrun module and the Kestrun.Certificates assembly to be loaded.
#>
function New-KrPrivateKeyJwt {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'ByCertificate_Authority')]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, ParameterSetName = 'ByCertificate_Authority')]
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, ParameterSetName = 'ByCertificate_EndPoint')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]
        $Certificate,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByJwkJson_Authority')]
        [Parameter(Mandatory = $true, ParameterSetName = 'ByJwkJson_EndPoint')]
        [string]
        $JwkJson,

        [Parameter(Mandatory = $true)]
        [string]
        $ClientId,


        [Parameter(Mandatory = $true, ParameterSetName = 'ByJwkJson_EndPoint')]
        [Parameter(Mandatory = $true, ParameterSetName = 'ByCertificate_EndPoint')]
        [string]
        $TokenEndpoint,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByJwkJson_Authority')]
        [Parameter(Mandatory = $true, ParameterSetName = 'ByCertificate_Authority')]
        [string]
        $Authority
    )
    process {
        if (-not $PSCmdlet.ShouldProcess("ClientId '$ClientId' to audience '$TokenEndpoint'", 'Generate private_key_jwt')) {
            return
        }
        $ResolvedTokenEndpoint = if ( -not ([string]::IsNullOrWhiteSpace($Authority))) {
            $discoUrl = ($Authority.TrimEnd('/')) + '/.well-known/openid-configuration'
            Write-KrLog -Level Debug -Message 'Discovering token endpoint from authority: {authority}' -Values $Authority

            try {
                $meta = Invoke-RestMethod -Uri $discoUrl -Method Get
            } catch {
                throw "Failed to fetch OIDC discovery document from '$discoUrl': $($_.Exception.Message)"
            }

            if (-not $meta.token_endpoint) {
                throw "OIDC discovery document from '$discoUrl' does not contain a token_endpoint."
            }

            Write-KrLog -Level Debug -Message 'Discovered token endpoint: {tokenEndpoint}' -Values $meta.token_endpoint
            [string]$meta.token_endpoint
        } else {
            $TokenEndpoint
        }

        if (-not [string]::IsNullOrWhiteSpace($JwkJson)) {

            Write-KrLog -Level Verbose -Message "Building private_key_jwt from JWK JSON for client '$ClientId'"
            return [Kestrun.Certificates.CertificateManager]::BuildPrivateKeyJwtFromJwkJson(
                $JwkJson,
                $ClientId,
                $ResolvedTokenEndpoint
            )
        }

        # ByCertificate / ByPath
        $cert = $Certificate

        if ($null -eq $cert) {
            throw 'Failed to obtain certificate instance.'
        }

        if (-not $cert.HasPrivateKey) {
            throw 'Certificate does not contain a private key; cannot build a private_key_jwt.'
        }

        Write-KrLog -Level Verbose -Message "Building private_key_jwt from certificate for client '$ClientId'"
        return [Kestrun.Certificates.CertificateManager]::BuildPrivateKeyJwt(
            $cert,
            $ClientId,
            $TokenEndpoint
        )
    }
}
