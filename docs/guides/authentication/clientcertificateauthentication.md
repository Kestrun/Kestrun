---
layout: default
title: Client Certificate Authentication
parent: Authentication
grand_parent: Guides
nav_order: 10
---

# Client Certificate Authentication

Use mutual TLS (mTLS) client certificates to authenticate callers.

This guide focuses on how client certificates work in Kestrun, how to configure Kestrel TLS behavior vs.
ASP.NET Core authentication, and how to make self-signed development setups work without weakening
production security.

---

## 1. Two layers: TLS handshake vs authentication

Client certificates involve two distinct layers:

1. **TLS handshake (Kestrel)**: the client presents a certificate during the TLS handshake.
   Kestrel decides whether to accept that certificate.
2. **Authentication (Kestrun / ASP.NET Core)**: once the request is established, the certificate is
   exposed as `Connection.ClientCertificate` and the certificate authentication handler creates an
   authenticated principal (claims).

If the TLS handshake rejects the certificate, the request never reaches Kestrun.

### Why this matters

- You can have `Add-KrClientCertificateAuthentication` configured correctly and still fail at TLS.
- Self-signed client certs often fail TLS validation by default unless you loosen validation or trust the issuing CA.

---

## 2. Quick start (development)

For a working, end-to-end example that:

- creates a self-signed server certificate,
- creates a self-signed client certificate,
- configures TLS to accept self-signed client certs in dev,
- requires certificate authentication only on selected routes,

use the tutorial sample:

- Tutorial sample: [`pwsh/tutorial/examples/8.8-Client-Certificate-Authentication.ps1`][8.8-Client-Certificate-Authentication.ps1]
- Tutorial page: [Client Certificate tutorial](/pwsh/tutorial/8.authentication/8.Client-Certificate)

---

## 3. Configure TLS client certificate handling (Kestrel)

Kestrel is configured via [Set-KrServerHttpsOptions][Set-KrServerHttpsOptions].

### 3.1 Client certificate mode

`-ClientCertificateMode` controls whether the client is required to present a certificate:

- `NoCertificate`: never request a client cert.
- `AllowCertificate`: request a client cert, but allow connections with none.
- `RequireCertificate`: fail the TLS handshake if the client does not present a certificate.

#### Recommendation

- Prefer `AllowCertificate` and enforce authentication on protected routes.
  This keeps public endpoints (like `/info` or health probes) working without a client cert.
- Use `RequireCertificate` only when you truly want every connection to be mTLS.

### 3.2 TLS validation callback (required for many dev setups)

Kestrel exposes a TLS validation callback:

- **Delegate**: `Func<X509Certificate2, X509Chain, SslPolicyErrors, bool>`

In Kestrun, you can provide it in three ways:

1. **Built-in .NET callbacks** (recommended when a simple policy is enough)
2. **Roslyn-compiled code snippet** (`-ClientCertificateValidationCode`)
3. **Roslyn-compiled code snippet from file** (`-ClientCertificateValidationCodePath`)

> **Important:** the TLS callback runs on Kestrel TLS threads during the handshake.
> It must be fast, thread-safe, and must not rely on PowerShell runspaces.

#### Option A: Built-in .NET callback

```powershell
Set-KrServerHttpsOptions `
  -ClientCertificateMode AllowCertificate `
  -ClientCertificateValidation ([Kestrun.Certificates.ClientCertificateValidationCallbacks]::AllowMissingOrSelfSignedForDevelopment)
```

#### Option B: Compile C# snippet (method body)

```powershell
$code = @'
if (certificate is null) return true; // AllowCertificate mode

return sslPolicyErrors == SslPolicyErrors.None
  || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors; // dev only
'@

Set-KrServerHttpsOptions `
  -ClientCertificateMode AllowCertificate `
  -ClientCertificateValidationLanguage CSharp `
  -ClientCertificateValidationCode $code
```

#### Option C: Compile from file path (language inferred)

- `.cs` / `.csx` => C#
- `.vb` => VB.NET

```powershell
Set-KrServerHttpsOptions `
  -ClientCertificateMode AllowCertificate `
  -ClientCertificateValidationCodePath './client-cert-validation.cs'
```

### 3.3 Endpoint-level requirement

If you want to require client certificates at the listener level, you can combine:

- `Set-KrServerHttpsOptions -ClientCertificateMode RequireCertificate`
- Listener configuration that requires a client certificate (when applicable)

However, route-level enforcement is usually more flexible and works well with `AllowCertificate`.

---

## 4. Configure certificate authentication (Kestrun)

Enable certificate auth via [Add-KrClientCertificateAuthentication][Add-KrClientCertificateAuthentication].

### 4.1 Common options

```powershell
Add-KrClientCertificateAuthentication `
  -AuthenticationScheme 'Certificate' `
  -AllowedCertificateTypes All `
  -RevocationMode NoCheck
```

Key points:

- `-AuthenticationScheme` lets you name the scheme used in routes.
- `-AllowedCertificateTypes` controls whether self-signed certs are acceptable.
- `-RevocationMode` controls CRL/OCSP behavior.

### 4.2 Route enforcement

To protect a route, specify the certificate scheme:

```powershell
Add-KrMapRoute -Verbs Get -Pattern '/secure/cert/hello' -AuthorizationScheme 'Certificate' -ScriptBlock {
  $cert = $Context.Connection.ClientCertificate
  Write-KrJsonResponse @{ subject = $cert.Subject; thumbprint = $cert.Thumbprint }
}
```

When using `AllowCertificate`, callers without a client certificate can still connect to the server,
but protected routes will return 401.

### 4.3 Claims

The certificate handler issues claims such as:

- `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name`
- `thumbprint`
- `issuer`
- `serialnumber`

Use `$Context.User.Claims` to inspect what was issued.

---

## 5. Trust and validation strategies

### 5.1 Production

Recommended production approach:

1. Use a client certificate issued by a trusted CA (internal PKI is fine).
2. Ensure the server trusts the CA chain (OS trust store or explicit trust store).
3. Prefer strict TLS validation (`SslPolicyErrors.None`) and enable revocation checking as appropriate.
4. Use certificate authentication to map identity and authorization.

Avoid accepting `RemoteCertificateChainErrors` in production.

### 5.2 Development / local

Options for local dev:

- Trust your dev CA in the OS trust store (best, closest to production behavior).
- Or relax TLS validation using the TLS callback (fastest for tutorials and CI).

If you relax TLS validation, keep it:

- scoped to local development environments,
- documented and easy to remove,
- paired with route-level auth enforcement.

---

## 6. Troubleshooting

| Symptom                                        | Likely cause                                                 | Fix |
| ---------------------------------------------- | ------------------------------------------------------------ | --- |
| Client: "The decryption operation failed"     | TLS handshake rejected the client certificate                | Configure TLS callback with [Set-KrServerHttpsOptions][Set-KrServerHttpsOptions] |
| Server: RemoteCertificateValidationCallback... | Self-signed/untrusted client cert fails chain validation     | Use a dev callback or trust the CA |
| "There is no Runspace available"              | TLS callback implemented as PowerShell script block          | Use a .NET delegate or Roslyn-compiled snippet |
| Public endpoints require a client certificate  | TLS mode is `RequireCertificate`                             | Prefer `AllowCertificate` and enforce per-route |
| 401 on protected route with a cert             | Auth scheme mismatch or cert auth not registered             | Align `-AuthenticationScheme` and `-AuthorizationScheme` |

---

## 7. References

- Guides:
  - [Certificates Management](/guides/certificates)
- Tutorials:
  - [Client Certificate tutorial](/pwsh/tutorial/8.authentication/8.Client-Certificate)
- PowerShell cmdlets:
  - [Set-KrServerHttpsOptions][Set-KrServerHttpsOptions]
  - [Add-KrEndpoint][Add-KrEndpoint]
  - [Add-KrClientCertificateAuthentication][Add-KrClientCertificateAuthentication]
  - [Add-KrMapRoute][Add-KrMapRoute]
  - [Enable-KrConfiguration][Enable-KrConfiguration]
  - [Start-KrServer][Start-KrServer]
  - [Import-KrCertificate][Import-KrCertificate]
  - [Export-KrCertificate][Export-KrCertificate]
  - [New-KrSelfSignedCertificate][New-KrSelfSignedCertificate]
- External:
  - [ASP.NET Core certificate authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth)
  - [X.509 (Wikipedia)](https://en.wikipedia.org/wiki/X.509)

---

Return to the [Authentication index](./index).

[8.8-Client-Certificate-Authentication.ps1]: /pwsh/tutorial/examples/8.8-Client-Certificate-Authentication.ps1
[Set-KrServerHttpsOptions]: /pwsh/cmdlets/Set-KrServerHttpsOptions
[Add-KrEndpoint]: /pwsh/cmdlets/Add-KrEndpoint
[Add-KrClientCertificateAuthentication]: /pwsh/cmdlets/Add-KrClientCertificateAuthentication
[Add-KrMapRoute]: /pwsh/cmdlets/Add-KrMapRoute
[Enable-KrConfiguration]: /pwsh/cmdlets/Enable-KrConfiguration
[Start-KrServer]: /pwsh/cmdlets/Start-KrServer
[Import-KrCertificate]: /pwsh/cmdlets/Import-KrCertificate
[Export-KrCertificate]: /pwsh/cmdlets/Export-KrCertificate
[New-KrSelfSignedCertificate]: /pwsh/cmdlets/New-KrSelfSignedCertificate
