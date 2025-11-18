---
layout: default
title: Certificates Management
parent: Guides
nav_order: 45
---

# Certificates & JWK Management

Kestrun provides a unified set of APIs and cmdlets to generate, import, export, convert, and validate X.509 certificates and JSON Web Keys (JWK). These tools support cross-platform scenarios (Windows, Linux, macOS) and are essential for HTTPS, JWT signing, OIDC/OAuth2 client authentication, and secure automation.

## Concepts

- **X.509 Certificate**: Standard for public key certificates (PEM, PFX, DER). Used for HTTPS, code signing, and authentication.
- **PEM**: Base64-encoded certificate or key (e.g., `-----BEGIN CERTIFICATE-----`).
- **PFX**: PKCS#12 archive (often `.pfx` or `.p12`) containing cert + private key, usually password-protected.
- **JWK**: JSON Web Key, a JSON format for representing cryptographic keys (used in OIDC, OAuth2, JWT, JWKS endpoints).
- **CSR**: Certificate Signing Request, used to request a certificate from a CA.

> See also: [OIDC guide](/guides/oidc) and [OAuth2 guide](/guides/oauth2) for JWK and client assertion usage.

---

## Common Scenarios

### 1. Self-signed certificate (PowerShell)

```powershell
$cert = New-KrSelfSignedCertificate -DnsNames localhost,127.0.0.1 -Exportable
Export-KrCertificate -Certificate $cert -FilePath './devcert' -Format Pfx -Password (ConvertTo-SecureString 'p@ss' -AsPlainText -Force) -IncludePrivateKey
Test-KrCertificate -Certificate $cert -DenySelfSigned:$false
```

### 2. Use with Kestrun server

```powershell
$server = New-KrServer -Name 'example'
Add-KrEndpoint -Port 5001 -X509Certificate $cert -Protocols Http1
```

### 3. JWK conversion and client assertion (OIDC/OAuth2)

```powershell
# Convert certificate to JWK (public or private)
Import-KrCertificate -FilePath './client.pfx' -Password (ConvertTo-SecureString 'p@ss' -AsPlainText -Force) |
    ConvertTo-KrJwkJson -IncludePrivateParameters > client.jwk.json

# Convert JWK to certificate
$jwk = Get-Content './client.jwk.json' -Raw
$cert = ConvertFrom-KrJwkJsonToCertificate -Jwk $jwk

# Build a private_key_jwt for OIDC/OAuth2 client authentication
$jwt = New-KrPrivateKeyJwt -JwkJson $jwk -ClientId 'my-client' -TokenEndpoint 'https://idp.example.com/oauth2/token'
```

### 4. Certificate Signing Request (CSR)

```powershell
$csrResult = New-KrCertificateRequest -DnsNames 'example.com','www.example.com' -Country US -Org 'Example Corp'
$csrResult.Csr | Set-Content './csr.pem'
$csrResult.PrivateKey | Set-Content './private.key'
```

### 5. C# Example: Self-signed

```csharp
var cert = CertificateManager.NewSelfSigned(new CertificateManager.SelfSignedOptions(
    DnsNames: new[] { "localhost", "127.0.0.1" }, KeyType: CertificateManager.KeyType.Rsa,
    KeyLength: 2048, ValidDays: 30, Exportable: true));
```

---

## Cmdlets & Functions

### Certificate lifecycle

- **New-KrSelfSignedCertificate**: Create a new self-signed certificate for dev/test.
- **New-KrCertificateRequest**: Generate a CSR and private key for CA signing.
- **Import-KrCertificate**: Load a certificate from PFX/PEM.
- **Export-KrCertificate**: Export a certificate to PFX or PEM (optionally with private key).
- **Test-KrCertificate**: Validate chain, EKU, and cryptographic strength.
- **Get-KrCertificatePurpose**: List Enhanced Key Usage (EKU) OIDs for a certificate.

### JWK and JWT integration

- **ConvertTo-KrJwkJson**: Convert a certificate or RSA private key PEM to JWK JSON (public or private).
- **ConvertFrom-KrJwkJsonToCertificate**: Create a self-signed X.509 certificate from an RSA JWK (private key required).
- **New-KrPrivateKeyJwt**: Build a signed JWT (private_key_jwt) for OIDC/OAuth2 client authentication using a certificate or JWK.

---

## Usage Patterns

### Convert certificate to JWK (public/private)

```powershell
# Public JWK (for JWKS endpoint)
Import-KrCertificate -FilePath './client.pfx' | ConvertTo-KrJwkJson

# Private JWK (for client assertion)
Import-KrCertificate -FilePath './client.pfx' | ConvertTo-KrJwkJson -IncludePrivateParameters
```

### Convert JWK to certificate

```powershell
$jwk = Get-Content './client.jwk.json' -Raw
$cert = ConvertFrom-KrJwkJsonToCertificate -Jwk $jwk
```

### Build private_key_jwt for OIDC/OAuth2

```powershell
$jwt = New-KrPrivateKeyJwt -JwkJson $jwk -ClientId 'my-client' -TokenEndpoint 'https://idp.example.com/oauth2/token'
```

### Generate CSR and private key

```powershell
$csrResult = New-KrCertificateRequest -DnsNames 'example.com' -Country US
$csrResult.Csr | Set-Content './csr.pem'
$csrResult.PrivateKey | Set-Content './private.key'
```

---

## Troubleshooting & Best Practices

- Always protect private keys and JWKs; never publish private JWKs or PEMs.
- Use strong key lengths (RSA 2048+ or ECDSA P-256+).
- Validate certificates with `Test-KrCertificate` before use in production.
- For OIDC/OAuth2, use JWKs for client assertion only if the provider supports `private_key_jwt`.
- When exporting to PEM, use `-IncludePrivateKey` only for secure, local use.
- For JWKS endpoints, export only public JWKs.
- Use `Get-KrCertificatePurpose` to check EKU for intended usage (e.g., server auth, client auth).

---

## References

- Cmdlets:
  - [New-KrSelfSignedCertificate](/pwsh/cmdlets/New-KrSelfSignedCertificate)
  - [New-KrCertificateRequest](/pwsh/cmdlets/New-KrCertificateRequest)
  - [Import-KrCertificate](/pwsh/cmdlets/Import-KrCertificate)
  - [Export-KrCertificate](/pwsh/cmdlets/Export-KrCertificate)
  - [Test-KrCertificate](/pwsh/cmdlets/Test-KrCertificate)
  - [Get-KrCertificatePurpose](/pwsh/cmdlets/Get-KrCertificatePurpose)
  - [ConvertTo-KrJwkJson](/pwsh/cmdlets/ConvertTo-KrJwkJson)
  - [ConvertFrom-KrJwkJsonToCertificate](/pwsh/cmdlets/ConvertFrom-KrJwkJsonToCertificate)
  - [New-KrPrivateKeyJwt](/pwsh/cmdlets/New-KrPrivateKeyJwt)
- Tutorials: [Certificates](/pwsh/tutorial/6.certificates/index)
- Guides: [OIDC](/guides/oidc), [OAuth2](/guides/oauth2)

---

Return to the [Guides index](./index).
