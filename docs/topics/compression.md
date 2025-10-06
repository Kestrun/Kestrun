---
layout: default
title: Response Compression
nav_order: 240
parent: Topics
---

# Response Compression

Kestrun supports transparent response compression (gzip and Brotli) via the
`Add-KrCompressionMiddleware` cmdlet and per–route opt-out using
`DisableResponseCompression` on `MapRouteOptions`.

## When to Use Compression

Enable compression for text-based payloads (JSON, XML, HTML, CSS, JS, YAML, plain text)
to reduce bandwidth and improve client perceived latency. Avoid compressing:

- Already compressed formats (JPEG, PNG, WebP, MP4, ZIP)
- Very small responses (overhead may outweigh the benefit)
- Highly dynamic data where CPU cost is critical and savings are minimal

## Enabling Compression

```powershell
New-KrServer -Name 'ApiServer' |
  Add-KrEndpoint -Port 5001 -SelfSignedCert |
  Add-KrCompressionMiddleware -EnableForHttps -MimeTypes 'text/plain','application/json','text/html' |
  Enable-KrConfiguration
```

Key switches:

- `-EnableForHttps` – allow compression over TLS (safe in modern runtime)
- `-MimeTypes` – explicit allow list (overrides defaults)
- `-ExcludedMimeTypes` – deny specific types even if broad wildcards are used
- `-DisableGzip` / `-DisableBrotli` – selectively remove providers (e.g. force only gzip or only brotli)

If neither disable switch is specified both providers are registered (order does not guarantee selection; the client `Accept-Encoding` drives negotiation).

## Disabling Specific Providers

To only allow Brotli (and not gzip):

```powershell
Add-KrCompressionMiddleware -DisableGzip -EnableForHttps
```

To only allow Gzip:

```powershell
Add-KrCompressionMiddleware -DisableBrotli -EnableForHttps
```

## Per-Route Opt-Out

Any route can opt out of compression by setting `DisableResponseCompression = $true`
inside route options:

```powershell
New-KrMapRouteOption -Property @{
  Pattern = '/metrics/raw'
  HttpVerbs = 'Get'
  Code = {
    # Prometheus style metrics text
    $lines = 1..500 | ForEach-Object { "metric_name{label='$_'} $_" }
    Write-KrTextResponse -InputObject ($lines -join "`n") -ContentType 'text/plain' -StatusCode 200
  }
  Language = 'PowerShell'
  DisableResponseCompression = $true
} | Add-KrMapRoute
```

This is useful for tooling expecting uncompressed streams, or where an upstream proxy already performs compression.

## Negotiation Flow

1. Client sends `Accept-Encoding: br, gzip, deflate`
2. Server middleware selects the first supported provider (Brotli > Gzip) matching the list
3. Response is compressed and `Content-Encoding` header set
4. If route opted-out compression middleware is bypassed

## Diagnostics & Troubleshooting

| Symptom | Possible Cause | Action |
|--------|----------------|--------|
| No `Content-Encoding` header | MIME not in list; body too small; route opted-out | Add/adjust `-MimeTypes`, generate larger payload, remove `DisableResponseCompression` |
| Large CPU usage | Compressing already-compressed assets | Use `-ExcludedMimeTypes 'image/*','application/zip'` |
| Client receives garbled text | Double compression by proxy | Ensure reverse proxy strips existing `Content-Encoding` before re-compressing |
| Brotli never used | Client didn't send `br` or disabled provider | Inspect request headers; remove `-DisableBrotli` |

## Testing Strategy

Use paired requests with and without `Accept-Encoding`:

```powershell
$raw = Invoke-WebRequest "$url/large" -UseBasicParsing -SkipCertificateCheck
$gz  = Invoke-WebRequest "$url/large" -UseBasicParsing -SkipCertificateCheck -Headers @{ 'Accept-Encoding'='gzip' }
$gz.Headers['Content-Encoding'] | Should -Be 'gzip'
($gz.RawContentLength -lt $raw.RawContentLength) | Should -BeTrue
```

For opt-out route:

```powershell
$no = Invoke-WebRequest "$url/large-nocompress" -UseBasicParsing -SkipCertificateCheck -Headers @{ 'Accept-Encoding'='gzip' }
$no.Headers['Content-Encoding'] | Should -BeNullOrEmpty
```

## Recommended MIME Set

```powershell
$common = 'text/plain','text/css','application/json','application/javascript','text/html','application/xml','text/xml','application/yaml','text/yaml'
Add-KrCompressionMiddleware -MimeTypes $common -EnableForHttps
```

## Security Notes

- Compression with TLS is safe; historical BREACH/CRIME style attacks rely on dynamic secret reflection—avoid reflecting secrets in compressed bodies
- Do not compress highly sensitive, tiny secret-bearing responses if they include attacker-controlled reflection

## Next Steps

- Combine with caching for maximum performance
- Add metrics around compression ratio to logs or observability pipeline
- Benchmark with and without Brotli to select optimal provider set for workload
