---
title: Certificates
parent: Guides
nav_order: 40
---

# Certificates (Create, Import, Export, Validate, Use in Kestrel)

Kestrun provides helper APIs and cmdlets to generate, import, export, and validate X.509 certificates.
They’re powered by Bouncy Castle and work the same on Windows, Linux, and macOS.

## Overview

You can:

- Create self‑signed certificates (RSA or ECDSA)
- Generate certificate signing requests (CSR)
- Import PFX/PEM/DER certificates
- Export to PFX/PEM with optional private keys and encryption
- Validate chains/usage and detect weak algorithms
- Inspect Enhanced Key Usage (EKU) values
- Use everything from PowerShell via simple cmdlets

Under the hood:

| C# Type / PS Module  | Purpose                                                                                                                                                              |
|----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `CertificateManager` | Static helper to create, import, export, and validate certificates.                                                                                                  |
| PowerShell           | Cmdlets `New-KrSelfSignedCertificate`, `New-KrCertificateRequest`, `Import-KrCertificate`, `Export-KrCertificate`, `Test-KrCertificate`, `Get-KrCertificatePurpose`. |

---

## C# quick examples

### 1. Creating a Self-Signed Certificate

```csharp
var cert = CertificateManager.NewSelfSigned(
    new CertificateManager.SelfSignedOptions(
        DnsNames: new[] { "localhost", "127.0.0.1" },
        KeyType: CertificateManager.KeyType.Rsa,
        KeyLength: 2048,
        ValidDays: 30,
        Exportable: true));
```

### 2. Generating a Certificate Request (CSR)

```csharp
var (csrPem, privateKey) = CertificateManager.NewCertificateRequest(
    new CertificateManager.CsrOptions(
        DnsNames: new[] { "example.com" },
        KeyType: CertificateManager.KeyType.Ecdsa,
        KeyLength: 384,
        Country: "US",
        Org: "Acme Ltd.",
        CommonName: "example.com"));
```

### 3. Importing / Exporting Certificates

```csharp
var imported = CertificateManager.Import("./devcert.pfx", "p@ss".AsSpan());

CertificateManager.Export(
    imported,
    filePath: "./devcert",
    fmt: CertificateManager.ExportFormat.Pfx,
    password: "p@ss".AsSpan(),
    includePrivateKey: true);
```

### 4. Validating Certificates

```csharp
bool ok = CertificateManager.Validate(
    imported,
    checkRevocation: false,
    allowWeakAlgorithms: false,
    denySelfSigned: false);
```

### 5. Using HTTPS with Kestrel

```csharp
var server = new KestrunHost();
server.ConfigureListener(
    port: 5001,
    ipAddress: IPAddress.Any,
    x509Certificate: imported,
    protocols: HttpProtocols.Http1AndHttp2);
server.ApplyConfiguration();
```

---

## PowerShell quick examples

```powershell
# 1. Create a dev certificate
$cert = New-KrSelfSignedCertificate -DnsNames localhost,127.0.0.1 -Exportable

# 2. Export it to a PFX file
Export-KrCertificate -Certificate $cert -FilePath './devcert' -Format Pfx `
    -Password (ConvertTo-SecureString 'p@ss' -AsPlainText -Force) -IncludePrivateKey

# 3. Validate before use
Test-KrCertificate -Certificate $cert -DenySelfSigned:$false

# 4. Configure listener
$server = New-KrServer -Name 'example'
Add-KrListener -Server $server -Port 5001 -X509Certificate $cert -Protocols Http1
```

---

For hands-on, step-by-step tutorials, see the Certificates section under the PowerShell tutorial.
