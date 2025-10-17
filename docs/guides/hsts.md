---
layout: default
title: HSTS and HTTPS Redirection
parent: Guides
nav_order: 6
---

# HSTS and HTTPS Redirection

HTTP Strict Transport Security (HSTS) and HTTPS redirection are essential security mechanisms for modern web applications.
This topic covers both features and how they work together to ensure secure communications.

## HTTPS Redirection

HTTPS redirection automatically redirects HTTP requests to their HTTPS equivalents, ensuring all traffic uses encrypted connections.

### Add-KrHttpsRedirection

Configures automatic redirection from HTTP to HTTPS endpoints.

```powershell
Add-KrHttpsRedirection [-RedirectStatusCode <int>] [-HttpsPort <int>] [-PassThru]
```

#### Parameters

- **RedirectStatusCode**: HTTP status code for redirects (default: 307)
  - `301` - Permanent redirect (cached by browsers)
  - `302` - Temporary redirect
  - `307` - Temporary redirect (preserves HTTP method)
  - `308` - Permanent redirect (preserves HTTP method)

- **HttpsPort**: Target HTTPS port (default: 443)

- **PassThru**: Returns the server instance for chaining

#### Examples

```powershell
# Basic HTTPS redirection
Add-KrHttpsRedirection

# Permanent redirect to custom port
Add-KrHttpsRedirection -RedirectStatusCode 301 -HttpsPort 8443

# With server chaining
New-KrServer | Add-KrHttpsRedirection -PassThru | Add-KrHsts
```

## HTTP Strict Transport Security (HSTS)

HSTS is a security policy that forces browsers to use HTTPS connections and prevents protocol downgrade attacks.

### Add-KrHsts

Configures HSTS headers and policies for secure communication.

```powershell
Add-KrHsts [-MaxAgeDays <int>] [-IncludeSubDomains] [-Preload] [-ExcludedHosts <string[]>] [-PassThru]
Add-KrHsts -Options <HstsOptions> [-PassThru]
```

#### HSTS Parameters

- **MaxAgeDays**: Duration browsers should enforce HSTS (default: 30 days)
- **IncludeSubDomains**: Apply policy to all subdomains
- **Preload**: Include site in browser preload lists
- **ExcludedHosts**: Hostnames excluded from HSTS policy
- **AllowInDevelopment**: Enable HSTS in development by clearing default excluded hosts
- **Options**: Pre-configured `HstsOptions` object
- **PassThru**: Returns the server instance for chaining

#### HSTS Examples

```powershell
# Basic HSTS (30 days)
Add-KrHsts

# Production configuration
Add-KrHsts -MaxAgeDays 365 -IncludeSubDomains -Preload

# Development/testing configuration
Add-KrHsts -MaxAgeDays 30 -IncludeSubDomains -Preload -AllowInDevelopment

# Exclude specific development hosts
Add-KrHsts -MaxAgeDays 1 -ExcludedHosts @('dev.example.com', 'staging.example.com')

# Using options object
$options = [Microsoft.AspNetCore.HttpsPolicy.HstsOptions]::new()
$options.MaxAge = [TimeSpan]::FromDays(730)  # 2 years
$options.IncludeSubDomains = $true
$options.Preload = $true
Add-KrHsts -Options $options
```

## Best Practices

### Security Configuration

#### Production Environment

```powershell
# Strict security for production
Add-KrHttpsRedirection -RedirectStatusCode 301 -HttpsPort 443
Add-KrHsts -MaxAgeDays 365 -IncludeSubDomains -Preload
```

#### Development Environment

```powershell
# Enable HSTS for development testing
Add-KrHttpsRedirection -RedirectStatusCode 307  # Temporary redirect
Add-KrHsts -MaxAgeDays 1 -IncludeSubDomains -AllowInDevelopment
```

Alternative approach with specific exclusions:

```powershell
# Exclude only specific development hosts
Add-KrHttpsRedirection -RedirectStatusCode 307
Add-KrHsts -MaxAgeDays 1 -ExcludedHosts @('dev.example.com', 'staging.example.com')
```

### Certificate Management

```powershell
# Development with self-signed certificate
$cert = New-KrSelfSignedCertificate -DnsNames localhost, 127.0.0.1 -ValidDays 30

# Production with CA certificate
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object Subject -Like "*example.com*"

# Configure endpoints
Add-KrEndpoint -Port 80    # HTTP
Add-KrEndpoint -Port 443 -X509Certificate $cert  # HTTPS
```

### Middleware Order

The order of middleware registration matters for security:

```powershell
# Correct order
Add-KrHttpsRedirection    # First: redirect to HTTPS
Add-KrHsts               # Second: set HSTS headers
Add-KrAntiforgery        # Third: other security middleware
Add-KrAuthentication     # Fourth: authentication
```

## Security Considerations

### HSTS Preload List

The preload directive submits your domain to browser preload lists:

**Benefits:**

- Protection before first visit
- Prevents initial HTTP connection
- Strongest security posture

**Considerations:**

- Difficult to remove once submitted
- Affects all subdomains if enabled
- Requires 2-year minimum max-age

```powershell
# Preload-ready configuration
Add-KrHsts -MaxAgeDays 730 -IncludeSubDomains -Preload
```

### Subdomain Implications

Including subdomains affects all current and future subdomains:

```powershell
# Affects all subdomains
Add-KrHsts -MaxAgeDays 365 -IncludeSubDomains

# Subdomain-specific exclusions
Add-KrHsts -MaxAgeDays 365 -IncludeSubDomains -ExcludedHosts @('api.example.com', 'dev.example.com')
```

### Development Considerations

Use shorter durations and appropriate configuration during development:

```powershell
# Development-friendly HSTS with AllowInDevelopment
Add-KrHsts -MaxAgeDays 1 -IncludeSubDomains -AllowInDevelopment

# Alternative: Explicit exclusions for specific development hosts
Add-KrHsts -MaxAgeDays 1 -ExcludedHosts @(
    'dev.example.com',
    'staging.example.com',
    'localhost.dev'
)
```

## Common Scenarios

### API Server

```powershell
# API with strict HSTS
New-KrServer -Name "API Server"
Add-KrEndpoint -Port 443 -X509Certificate $cert
Add-KrHsts -MaxAgeDays 730  # 2 years for API stability
Add-KrMapRoute -Pattern "/api/*" -ScriptBlock { Write-KrJsonResponse @{status="OK"} }
```

### Web Application

```powershell
# Web app with HTTPS redirection
New-KrServer -Name "Web App"
Add-KrEndpoint -Port 80     # HTTP for redirects
Add-KrEndpoint -Port 443 -X509Certificate $cert
Add-KrHttpsRedirection -RedirectStatusCode 301
Add-KrHsts -MaxAgeDays 365 -IncludeSubDomains
```

### Microservice

```powershell
# Internal microservice
New-KrServer -Name "Internal Service"
Add-KrEndpoint -Port 8443 -X509Certificate $cert
Add-KrHsts -MaxAgeDays 90  # Shorter for internal services
```

## Testing and Verification

### Verify HTTPS Redirection

```powershell
# Test HTTP to HTTPS redirect
$response = Invoke-WebRequest -Uri "http://localhost:8080" -MaximumRedirection 0 -ErrorAction SilentlyContinue
$response.StatusCode        # Should be 301, 302, 307, or 308
$response.Headers.Location  # Should be HTTPS URL
```

### Verify HSTS Headers

```powershell
# Check HSTS header
$response = Invoke-WebRequest -Uri "https://localhost:8443" -SkipCertificateCheck
$hstsHeader = $response.Headers['Strict-Transport-Security']
Write-Host "HSTS Header: $hstsHeader"
# Example: "max-age=31536000; includeSubDomains; preload"
```

### Browser Testing

1. **Clear browser cache** for the test domain
2. **Visit HTTP URL** and verify redirect to HTTPS
3. **Check browser security indicators** (lock icon)
4. **Verify HSTS behavior** by typing HTTP URL (should auto-upgrade)

## Troubleshooting

### Common Issues

**HSTS not enforced in browser:**

- Clear browser cache and site data
- Verify HTTPS endpoint is accessible
- Check certificate validity
- Ensure HSTS header is present

**Mixed content warnings:**

- Ensure all resources (CSS, JS, images) use HTTPS
- Use protocol-relative URLs (`//example.com/style.css`)
- Check for hardcoded HTTP URLs in content

**Certificate errors:**

- Verify certificate matches hostname
- Check certificate expiration
- Ensure certificate chain is complete
- For development, use `-SkipCertificateCheck` for testing only

### Development Tools

```powershell
# Check certificate details
$cert = New-KrSelfSignedCertificate -DnsNames localhost
$cert | Format-List Subject, NotBefore, NotAfter, Thumbprint

# Test HTTPS endpoint
Test-NetConnection -ComputerName localhost -Port 443

# Verify TLS configuration
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
```

## References

- [RFC 6797 - HTTP Strict Transport Security](https://tools.ietf.org/html/rfc6797)
- [OWASP HSTS Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Strict_Transport_Security_Cheat_Sheet.html)
- [MDN HSTS Documentation](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Strict-Transport-Security)
- [Add-KrHttpsRedirection Tutorial](../pwsh/tutorial/10.middleware/3.Https-Redirection.md)
- [Add-KrHsts Tutorial](../pwsh/tutorial/10.middleware/4.Https-Hsts.md)
- [Certificate Management](../pwsh/tutorial/6.certificates/index.md)
- [AspNetCoreSSL](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl)
- [HSTS](https://developer.mozilla.org/docs/Web/HTTP/Headers/Strict-Transport-Security)

---

Return to the [Guides index](./index).
