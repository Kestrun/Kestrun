---
layout: default
title: Forwarded Headers
parent: Topics
nav_order: 7
---

# Forwarded Headers

Reverse proxies (Nginx, Apache, cloud load balancers) often sit in front of your app. They terminate TLS and forward requests over HTTP.
To preserve the original client details, proxies add headers such as X-Forwarded-For, X-Forwarded-Proto, and X-Forwarded-Host.
This topic explains what these headers are, why you should honor them, and how to configure Kestrun + your proxy correctly.

## What are "forwarded headers"?

Forwarded headers convey original request information when a proxy is in the path:

- X-Forwarded-For: the client IP (and optionally a chain of upstream proxies)
- X-Forwarded-Proto: the original scheme (http/https)
- X-Forwarded-Host: the original Host header (domain[:port])
- X-Forwarded-PathBase: the original path base when the app is mounted under a sub-path
- Forwarded (RFC 7239): a standardized alternative that can carry similar data in a single header

In ASP.NET Core, the Forwarded Headers middleware reads these headers and updates HttpContext so your app sees the “external” values
(Request.Scheme, Request.Host, Connection.RemoteIpAddress, PathBase).

## Why honor them?

- Correct URL generation: Links, redirects, and absolute URLs must reflect the public scheme/host.
- Security decisions: Auth callbacks, cookie policies, and HSTS/HTTPS expectations depend on the external scheme/domain.
- Accurate logging/telemetry: See the real client IP rather than the proxy’s IP.
- Multi-tenant + sub-path hosting: Preserve PathBase for apps mounted under /app or similar.

> Important: Only trust headers from known proxies. Never allow arbitrary clients to spoof X-Forwarded-*.

## Kestrun configuration

Enable Forwarded Headers early in the pipeline and restrict trust to your proxy addresses or networks.

### PowerShell

```powershell
Add-KrForwardedHeader -XForwardedFor -XForwardedProto -XForwardedHost -ForwardLimit 1 -KnownProxies @('127.0.0.1', '::1')
```

Inside a route, you can read the effective values:

```powershell
$scheme   = $Context.Request.Scheme
$requestHost     = $Context.Request.Host
$clientIp = $Context.Connection.RemoteIpAddress
```

### C# (sketch)

```csharp
using Microsoft.AspNetCore.HttpOverrides;

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    ForwardLimit = 1,
    KnownProxies = { IPAddress.Parse("127.0.0.1"), IPAddress.IPv6Loopback }
});
```

## Proxy configuration examples

Below are minimal proxy snippets that forward the needed headers correctly.

### Nginx (reverse proxy)

```nginx
server {
  listen 80;
  server_name example.com;

  location / {
    proxy_pass http://127.0.0.1:5000;

    # Preserve client IP and public scheme/host
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-Host  $host;

    # Optional: if app is mounted under /app
    # proxy_set_header X-Forwarded-PathBase /app;
  }
}
```

### Apache httpd (mod_proxy)

```apache
<VirtualHost *:80>
  ServerName example.com

  ProxyPreserveHost On
  ProxyPass        / http://127.0.0.1:5000/
  ProxyPassReverse / http://127.0.0.1:5000/

  RequestHeader set X-Forwarded-Proto expr=%{REQUEST_SCHEME}
  RequestHeader set X-Forwarded-Host  "%{HTTP_HOST}e" env=HTTP_HOST
  # X-Forwarded-For is added automatically when using mod_proxy; else:
  # RequestHeader add X-Forwarded-For "%{REMOTE_ADDR}e"
</VirtualHost>
```

### HAProxy

```haproxy
frontend fe_http
  bind *:80
  mode http
  default_backend be_app

backend be_app
  mode http
  server app1 127.0.0.1:5000 check
  http-request set-header X-Forwarded-Proto http if !{ ssl_fc }
  http-request set-header X-Forwarded-Proto https if { ssl_fc }
  http-request set-header X-Forwarded-Host %[req.hdr(Host)]
  http-request add-header X-Forwarded-For %[src]
```

### Azure Front Door / Application Gateway

Most managed cloud proxies add X-Forwarded-For and X-Forwarded-Proto automatically.
Ensure your Kestrun app trusts only those proxy IPs (or subnets):

```powershell
# Example: trust provider CIDR ranges (replace with actual)
Add-KrForwardedHeader -XForwardedFor -XForwardedProto -XForwardedHost -KnownNetworks @('192.0.2.0/24', '2001:db8::/32')
```

Consult your provider’s documentation for the exact outbound IP ranges and update periodically.

## Security considerations

- Trust boundary: Set `KnownProxies` and/or `KnownNetworks`. Do not clear these in production.
- Limit depth: Keep `ForwardLimit` small (often 1) unless you expect multi-proxy chains you control.
- Host validation: Combine with Host Filtering to prevent Host header attacks.
- HTTPS expectations: When offloading TLS at the proxy, set/validate `X-Forwarded-Proto` so cookies and redirects are correct.
- Logging: Log the effective client IP and the raw X-Forwarded-* values for incident analysis.

## Troubleshooting

| Symptom | Cause | Resolution |
|---------|-------|------------|
| Request.Scheme remains http | Middleware runs too late | Register forwarded headers before routing/configuration. |
| `RemoteIpAddress` is proxy IP | Headers not forwarded or not trusted | Verify proxy sets X-Forwarded-For and add proxy IP to KnownProxies/Networks. |
| Wrong Host in links | Missing X-Forwarded-Host | Forward Host and enable XForwardedHost flag. |
| PathBase lost behind sub-path | Missing X-Forwarded-PathBase support | Forward header and enable flag; alternatively, rewrite path at proxy. |
| Spoofed client IP risk | Headers trusted from the Internet | Lock down trust to proxy addresses only. |

## Related

- PowerShell tutorial: [Forwarded Headers](/pwsh/tutorial/10.middleware/7.Forwarded-Headers)
- Cmdlet: [Add-KrForwardedHeader](/pwsh/cmdlets/Add-KrForwardedHeader)
- Topic: [Host Filtering](./hsts)
- ASP.NET Core docs: [Forwarded Headers Middleware](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer)

## External references

- [RFC 7239: Forwarded HTTP Extension](https://www.rfc-editor.org/rfc/rfc7239)
- [MDN: X-Forwarded-For](https://developer.mozilla.org/docs/Web/HTTP/Headers/X-Forwarded-For)
- [MDN: X-Forwarded-Proto](https://developer.mozilla.org/docs/Web/HTTP/Headers/X-Forwarded-Proto)
- [MDN: X-Forwarded-Host](https://developer.mozilla.org/docs/Web/HTTP/Headers/X-Forwarded-Host)
- [Nginx: ngx_http_proxy_module](https://nginx.org/en/docs/http/ngx_http_proxy_module.html)
- [Apache: mod_proxy](https://httpd.apache.org/docs/current/mod/mod_proxy.html)
- [HAProxy Docs](https://docs.haproxy.org/)
- [Azure Front Door headers](https://learn.microsoft.com/azure/frontdoor/front-door-http-headers-protocol)
