---
layout: default
title: Certificates Management
parent: Guides
nav_order: 30
---

# Certificates & JWK Management

Kestrun provides a unified set of APIs and cmdlets to generate, import, export, convert, and validate X.509 certificates and JSON Web Keys (JWK).
These tools support cross-platform scenarios (Windows, Linux, macOS) and are essential for HTTPS, JWT signing, OIDC/OAuth2 client authentication, and secure automation.

## Concepts

- **X.509 Certificate**: Standard for public key certificates (PEM, PFX, DER). Used for HTTPS, code signing, and authentication.
- **PEM**: Base64-encoded certificate or key (e.g., `-----BEGIN CERTIFICATE-----`).
- **PFX**: PKCS#12 archive (often `.pfx` or `.p12`) containing cert + private key, usually password-protected.
- **JWK**: JSON Web Key, a JSON format for representing cryptographic keys (used in OIDC, OAuth2, JWT, JWKS endpoints).
- **CSR**: Certificate Signing Request, used to request a certificate from a CA.

> See also: [OIDC guide](/guides/authentication/oidc) and [OAuth2 guide](/guides/authentication/oauth2) for JWK and client assertion usage.

---

## Common Scenarios

### 1. Development CA and issuer-signed leaf certificate (PowerShell)

```powershell
$bundle = New-KrDevelopmentCertificate -TrustRoot
$cert = $bundle.LeafCertificate
$root = $bundle.RootCertificate

Export-KrCertificate -Certificate $cert -FilePath './devcert' -Format Pfx -Password (ConvertTo-SecureString 'p@ss' -AsPlainText -Force) -IncludePrivateKey
$valid = Test-KrCertificate -Certificate $cert -CertificateChain $root -FailureReasonVariable 'reason' -DenySelfSigned:$false
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
var cert = CertificateManager.NewSelfSigned(new SelfSignedOptions(
    DnsNames: new[] { "localhost", "127.0.0.1", "::1" },
    KeyType: KeyType.Rsa,
    KeyLength: 2048,
    KeyUsageFlags: X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
    ValidDays: 30,
    Exportable: true));
```

### 6. Self-signed certificate with explicit KU

```powershell
$cert = New-KrSelfSignedCertificate `
    -DnsNames 'localhost','127.0.0.1','::1' `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -Exportable
```

For ECDSA, prefer `DigitalSignature` only:

```powershell
$cert = New-KrSelfSignedCertificate `
    -DnsNames 'localhost','::1' `
    -KeyType Ecdsa `
    -KeyLength 256 `
    -KeyUsage DigitalSignature
```

### 7. Development root CA + localhost leaf

```powershell
$root = New-KrSelfSignedCertificate `
    -DnsNames 'Kestrun Development Root CA' `
    -CertificateAuthority `
    -Exportable `
    -ValidDays 3650

$leaf = New-KrSelfSignedCertificate `
    -DnsNames 'localhost','127.0.0.1','::1' `
    -IssuerCertificate $root `
    -Exportable `
    -ValidDays 30
```

Or use the convenience helper:

```powershell
$bundle = New-KrDevelopmentCertificate -TrustRoot
$leaf = $bundle.LeafCertificate
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

### Localhost HTTPS for browsers

For localhost development, prefer including all loopback names you expect to hit:

```powershell
$cert = New-KrSelfSignedCertificate `
    -DnsNames 'localhost','127.0.0.1','::1' `
    -KeyUsage DigitalSignature,KeyEncipherment `
    -Exportable
```

Kestrun emits:

- `DNS:localhost`
- `IP:127.0.0.1`
- `IP:::1`

IP addresses are emitted as IP SAN entries, not DNS SAN entries.

For the default RSA localhost leaf, Kestrun also emits:

- Subject: `CN=localhost`
- Basic Constraints: `CA:FALSE`
- EKU: `Server Authentication`, `Client Authentication`
- Key Usage: `DigitalSignature`, `KeyEncipherment`

This certificate shape is valid for local HTTPS and works with tools such as `curl -k`.
Firefox also commonly accepts the scenario once you choose to trust the certificate locally.
On Windows, Chromium-family browsers can still reject a raw self-signed localhost leaf with
`ERR_CERT_INVALID` even when the certificate contents are otherwise correct.

### CA root versus issuer-signed leaf

Use the certificate creation flags this way:

- `-CertificateAuthority`: create a CA root or intermediate certificate that can sign child certificates.
- `-IssuerCertificate`: create a leaf certificate signed by an existing CA/root certificate.

Typical localhost browser workflow:

1. Create or import a development CA root.
2. Optionally trust that root in the Windows CurrentUser Root store.
3. Issue a localhost leaf certificate from that root.
4. Bind the leaf certificate to the Kestrun HTTPS listener.

If you want that in one call, use `New-KrDevelopmentCertificate`.

### Validate issuer-signed development certificates

`New-KrDevelopmentCertificate` creates a localhost leaf certificate signed by a private development root.
If that root is not trusted by the operating system, `Test-KrCertificate` can report `PartialChain` even though
the leaf certificate itself is correct.

For validation in scripts, tests, or local tooling, pass the root certificate explicitly:

```powershell
$bundle = New-KrDevelopmentCertificate -Exportable
$leaf = $bundle.LeafCertificate
$root = $bundle.RootCertificate

$valid = Test-KrCertificate -Certificate $leaf -CertificateChain $root -FailureReasonVariable 'reason'

if (-not $valid) {
    Write-Host "Validation failed: $reason"
}
```

When the root is already trusted, the explicit chain is usually unnecessary. When it is not trusted,
`-CertificateChain` lets validation succeed without modifying the machine certificate store.

### Reuse the root and leaf across sessions

If you want a stable local development setup, export both certificates to PFX once, then import the root in later sessions and issue new leaves from it as needed.

```powershell
$password = ConvertTo-SecureString 'p@ssw0rd!' -AsPlainText -Force
$bundle = New-KrDevelopmentCertificate -Exportable

New-Item -ItemType Directory -Force './certs' | Out-Null

Export-KrCertificate -Certificate $bundle.RootCertificate -FilePath './certs/dev-root' -Format Pfx -Password $password -IncludePrivateKey
Export-KrCertificate -Certificate $bundle.LeafCertificate -FilePath './certs/localhost' -Format Pfx -Password $password -IncludePrivateKey
```

Later, reuse the exported root certificate to issue a fresh localhost leaf:

```powershell
$password = ConvertTo-SecureString 'p@ssw0rd!' -AsPlainText -Force
$root = Import-KrCertificate -FilePath './certs/dev-root.pfx' -Password $password

$bundle = New-KrDevelopmentCertificate `
    -RootCertificate $root `
    -DnsNames 'localhost','127.0.0.1','::1' `
    -Exportable

$leaf = $bundle.LeafCertificate
```

You can also import the previously exported localhost leaf directly if you want to keep the same certificate material across restarts:

```powershell
$password = ConvertTo-SecureString 'p@ssw0rd!' -AsPlainText -Force
$leaf = Import-KrCertificate -FilePath './certs/localhost.pfx' -Password $password
```

Treat the exported root PFX as a sensitive signing key. Store it securely, use a strong password, and avoid distributing it beyond your local development environment.

If you import the leaf later and want to validate it outside the trusted OS store, also import the root and pass it with `-CertificateChain`:

```powershell
$password = ConvertTo-SecureString 'p@ssw0rd!' -AsPlainText -Force
$leaf = Import-KrCertificate -FilePath './certs/localhost.pfx' -Password $password
$root = Import-KrCertificate -FilePath './certs/dev-root.pfx' -Password $password

$valid = Test-KrCertificate -Certificate $leaf -CertificateChain $root -FailureReasonVariable 'reason'
```

### Windows Chromium workflow

For the most reliable Edge / Chrome / Brave development workflow on Windows, use a trusted
development certificate from the Windows trust store instead of a raw self-signed localhost leaf.

Create and trust an ASP.NET Core development certificate on Windows:

```powershell
$certDir = "$env:USERPROFILE\.aspnet\https"
New-Item -ItemType Directory -Force $certDir | Out-Null

dotnet dev-certs https --trust
dotnet dev-certs https -ep "$certDir\kestrun-devcert.pfx" -p testpass
```

Then load that certificate into Kestrun:

```powershell
$cert = Import-KrCertificate `
    -FilePath 'C:\Users\<YourUser>\.aspnet\https\kestrun-devcert.pfx' `
    -Password (ConvertTo-SecureString 'testpass' -AsPlainText -Force)

$server = New-KrServer -Name 'example'
Add-KrEndpoint -Port 5001 -X509Certificate $cert -Protocols Http1
```

If your Kestrun process runs inside WSL but your browser runs on Windows, trust must exist in the
Windows certificate store because Chromium uses the Windows trust chain for validation.

### EKU and KU defaults

Kestrun self-signed development certificates default to:

- EKU: `Server Authentication`, `Client Authentication`
- RSA KU: `DigitalSignature`, `KeyEncipherment`
- ECDSA KU: `DigitalSignature`

Use `-KeyUsage` when you need to override KU explicitly for a specific integration.

---

## Troubleshooting & Best Practices

- Always protect private keys and JWKs; never publish private JWKs or PEMs.
- Use strong key lengths (RSA 2048+ or ECDSA P-256+).
- Validate certificates with `Test-KrCertificate` before use in production.
- For issuer-signed development certificates, pass the issuing root with `-CertificateChain` unless that root is already trusted by the platform.
- For OIDC/OAuth2, use JWKs for client assertion only if the provider supports `private_key_jwt`.
- When exporting to PEM, use `-IncludePrivateKey` only for secure, local use.
- For JWKS endpoints, export only public JWKs.
- Use `Get-KrCertificatePurpose` to check EKU for intended usage (e.g., server auth, client auth).
- For Chromium-family browsers on Windows, a raw self-signed leaf may still be rejected even if Firefox accepts it.
- For the most reliable Windows browser workflow, prefer a local dev CA trusted in the Windows root store, then issue localhost leaf certs from that CA.
- If you use `-IssuerCertificate`, the issuer must be a CA certificate and its private key must support PKCS#8 export.
- If `curl -k` succeeds and Firefox loads the site, but Edge / Chrome / Brave show
    `ERR_CERT_INVALID`, treat that as a browser trust/store issue first, not proof
    that the served certificate is malformed.
- If `Test-KrCertificate` reports `PartialChain` for a localhost certificate issued by
    `Kestrun Development Root CA`, the usual cause is that the root is not trusted or was not
    supplied with `-CertificateChain`.
- If you use a self-signed localhost leaf, prefer HTTP/1.1 + HTTP/2 during development and verify trust behavior separately before enabling HTTP/3.

---

## References

- Cmdlets:
  - [New-KrSelfSignedCertificate](/pwsh/cmdlets/New-KrSelfSignedCertificate)
    - [New-KrDevelopmentCertificate](/pwsh/cmdlets/New-KrDevelopmentCertificate)
  - [New-KrCertificateRequest](/pwsh/cmdlets/New-KrCertificateRequest)
  - [Import-KrCertificate](/pwsh/cmdlets/Import-KrCertificate)
  - [Export-KrCertificate](/pwsh/cmdlets/Export-KrCertificate)
  - [Test-KrCertificate](/pwsh/cmdlets/Test-KrCertificate)
  - [Get-KrCertificatePurpose](/pwsh/cmdlets/Get-KrCertificatePurpose)
  - [ConvertTo-KrJwkJson](/pwsh/cmdlets/ConvertTo-KrJwkJson)
  - [ConvertFrom-KrJwkJsonToCertificate](/pwsh/cmdlets/ConvertFrom-KrJwkJsonToCertificate)
  - [New-KrPrivateKeyJwt](/pwsh/cmdlets/New-KrPrivateKeyJwt)
- Tutorials: [Certificates](/pwsh/tutorial/6.certificates/index)
- Guides: [OIDC](/guides/authentication/oidc), [OAuth2](/guides/authentication/oauth2)

---

Return to the [Guides index](./index).
