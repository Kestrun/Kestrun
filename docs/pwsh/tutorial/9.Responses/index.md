---
title: Responses
parent: Tutorials
nav_order: 9
---

# Responses Overview

Return data to clients using purpose-built helpers for common content types:
text, JSON, YAML, XML, CSV, HTML, files, streams, binary, BSON, CBOR. They
also simplify redirects, errors and HTTP caching primitives.

These cmdlets wrap the underlying ASP.NET Core primitives. They keep route
script blocks concise while still letting you customize status codes,
headers and serialization.

## Chapters

| # | Topic | Highlights |
|---|-------|------------|
| 1 | [Text & JSON](./1.Basic-Text-Json) | Plain text, JSON serialization depth & compression |
| 2 | [Structured (XML / YAML / CSV)](./2.Structured-Xml-Yaml-Csv) | Alternative serializations, custom content types |
| 3 | [Binary & Stream](./3.Binary-Stream) | Raw bytes, streaming large content |
| 4 | [HTML Templates & Files](./4.Html-Templates-Files) | File + template rendering, file download headers |
| 5 | [Special Formats (BSON / CBOR)](./5.Special-Bson-Cbor) | Compact binary object encodings |
| 6 | [Redirects](./6.Redirects) | 3xx responses + optional message body |
| 7 | [Errors](./7.Errors) | Consistent error payloads, exceptions & stack traces |
| 8 | [Caching & Revalidation](./8.Caching) | Cache-Control, ETag, Last-Modified, 304 handling |
| 9 | [Low-Level Response Stream](./9.Low-Level-Response) | Direct stream writing when you need full control |

## Quick Start

Minimal server returning JSON & text:

```powershell
New-KrServer -Name 'Responses Demo' | Out-Null
Add-KrEndpoint -Url 'http://127.0.0.1:5055' | Out-Null
Add-KrPowerShellRuntime | Out-Null

Add-KrMapRoute -Path '/hello' -Method GET -ScriptBlock {
  Write-KrTextResponse 'Hello world from text'
}
Add-KrMapRoute -Path '/data' -Method GET -ScriptBlock {
  [PSCustomObject]@{ message = 'Hi'; time = (Get-Date) } | Write-KrJsonResponse -Compress
}

Enable-KrConfiguration
Start-KrServer | Out-Null
```

Then:

```powershell
curl http://127.0.0.1:5055/hello
curl http://127.0.0.1:5055/data
```

## Cmdlets Covered

- `Write-KrTextResponse`
- `Write-KrJsonResponse`
- `Write-KrXmlResponse`
- `Write-KrYamlResponse`
- `Write-KrCsvResponse`
- `Write-KrHtmlResponse`
- `Write-KrFileResponse`
- `Write-KrBinaryResponse`
- `Write-KrStreamResponse`
- `Write-KrBsonResponse`
- `Write-KrCborResponse`
- `Write-KrRedirectResponse`
- `Write-KrErrorResponse`
- `Add-KrCacheResponse`
- `Test-KrCacheRevalidation`
- `Write-KrResponse` (low-level)

## Choosing a Helper

| Scenario | Use |
|----------|-----|
| Simple text or diagnostics | `Write-KrTextResponse` |
| API returning objects | `Write-KrJsonResponse` |
| Legacy / interop requirements | `Write-KrXmlResponse` |
| Configuration style payloads | `Write-KrYamlResponse` |
| Tabular export | `Write-KrCsvResponse` |
| Page or fragment templating | `Write-KrHtmlResponse` |
| Download / inline file | `Write-KrFileResponse` |
| Large file / custom generation | `Write-KrStreamResponse` |
| Binary blob already buffered | `Write-KrBinaryResponse` |
| High density object format | `Write-KrBsonResponse` or `Write-KrCborResponse` |
| Set Location & 3xx status | `Write-KrRedirectResponse` |
| Consistent problem payload | `Write-KrErrorResponse` |
| Cache headers (max-age etc.) | `Add-KrCacheResponse` |
| ETag / 304 negotiation | `Test-KrCacheRevalidation` |
| Full control (edge cases) | `Write-KrResponse` |

---

### Next

{: .fs-4 .fw-500}

Previous: [Authentication](../8.authentication/index)
Next: [Text & JSON](./1.Basic-Text-Json)
