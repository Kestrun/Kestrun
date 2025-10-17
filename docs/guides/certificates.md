---
layout: default
title: Certificates Management
parent: Guides
nav_order: 45
---

# Certificates Management

Kestrun provides helper APIs and cmdlets to generate, import, export, and validate X.509 certificates.
They work cross‑platform on Windows, Linux, and macOS.

## Quick Examples (PowerShell)

```powershell
$cert = New-KrSelfSignedCertificate -DnsNames localhost,127.0.0.1 -Exportable
Export-KrCertificate -Certificate $cert -FilePath './devcert' -Format Pfx -Password (ConvertTo-SecureString 'p@ss' -AsPlainText -Force) -IncludePrivateKey
Test-KrCertificate -Certificate $cert -DenySelfSigned:$false
```

Use with Kestrun:

```powershell
$server = New-KrServer -Name 'example'
Add-KrEndpoint -Port 5001 -X509Certificate $cert -Protocols Http1
```

## Quick Examples (C#)

```csharp
var cert = CertificateManager.NewSelfSigned(new CertificateManager.SelfSignedOptions(
    DnsNames: new[] { "localhost", "127.0.0.1" }, KeyType: CertificateManager.KeyType.Rsa,
    KeyLength: 2048, ValidDays: 30, Exportable: true));
```

## References

- Cmdlets: `New-KrSelfSignedCertificate`, `New-KrCertificateRequest`, `Import-KrCertificate`, `Export-KrCertificate`, `Test-KrCertificate`, `Get-KrCertificatePurpose`
- Tutorial: [Certificates](/pwsh/tutorial/6.certificates/index)

---

Return to the [Guides index](./index).
