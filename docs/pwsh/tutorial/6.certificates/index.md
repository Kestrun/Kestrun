---
title: Certificates
parent: Tutorials
nav_order: 7
---

# Kestrun Certificates

> 🚧 **Work in Progress**
>
> This page is currently under development. Content will be expanded with guides, examples, and best practices soon.
> Thank you for your patience while we build it out.

## Overview

Kestrun provides helper APIs to generate, import, export and validate X.509 certificates.
These utilities build on Bouncy Castle so they run the same on Windows, Linux and macOS. They allow you to:

* **Create self‑signed certificates** with RSA or ECDSA keys.
* **Generate certificate requests (CSR)** for signing by a real CA.
* **Import certificates** from PFX, PEM or DER files.
* **Export certificates** back to PFX or PEM with optional private keys and encryption.
* **Validate certificates** — chain building, key usages and weak algorithm checks.
* **Inspect Enhanced Key Usage (EKU)** values.
* **Use the same features from PowerShell** via simple cmdlets.

Under the hood we provide:

| C# Type / PS Module | Purpose |
|-----------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| `CertificateManager` | Static helper with methods to create, import, export and validate certificates. |
| **PowerShell** | Cmdlets `New-KrSelfSignedCertificate`, `New-KrCertificateRequest`, `Import-KrCertificate`, `Export-KrCertificate`, `Test-KrCertificate`, `Get-KrCertificatePurpose`. |

---

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

### 3. Importing Certificates

```csharp
var imported = CertificateManager.Import("./devcert.pfx", "p@ss".AsSpan());
```

### 4. Exporting Certificates

```csharp
CertificateManager.Export(
    imported,
    filePath: "./devcert",
    fmt: CertificateManager.ExportFormat.Pfx,
    password: "p@ss".AsSpan(),
    includePrivateKey: true);
```

### 5. Validating Certificates

```csharp
bool ok = CertificateManager.Validate(
    imported,
    checkRevocation: false,
    allowWeakAlgorithms: false,
    denySelfSigned: false);
```

### 6. Using HTTPS with Kestrel

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

## PowerShell Usage

```powershell
# 1. Create a dev certificate
$cert = New-KrSelfSignedCertificate -DnsName localhost,127.0.0.1 -Exportable

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

## PowerShell Cmdlet Reference

| Cmdlet | What it does | Typical pipeline position |
|---------------------------------------|--------------------------------------------------------------------------|---------------------------|
| **`New-KrSelfSignedCertificate`** | Creates a self‑signed RSA/ECDSA certificate. | n/a |
| **`New-KrCertificateRequest`** | Builds a PEM encoded CSR and returns the private key. | n/a |
| **`Import-KrCertificate`** | Imports a PFX/PEM/DER certificate file. | n/a |
| **`Export-KrCertificate`** | Exports a certificate to PFX or PEM format. | n/a |
| **`Test-KrCertificate`** | Validates a certificate’s chain and strength. | n/a |
| **`Get-KrCertificatePurpose`** | Lists the EKU values on a certificate. | n/a |
