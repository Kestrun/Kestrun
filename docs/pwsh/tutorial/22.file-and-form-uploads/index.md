---
layout: default
parent: Tutorials
title: File and Form Uploads
nav_order: 22
has_children: true
---

# File and Form Uploads

Parse multipart and form submissions with streaming storage and limits.

## Chapters

1. [Basic multipart/form-data upload](./22.1-Basic-Multipart)
2. [Multiple files (same field name)](./22.2-Multiple-Files)
3. [application/x-www-form-urlencoded forms](./22.3-UrlEncoded)
4. [multipart/mixed ordered parts](./22.4-Multipart-Mixed)
5. [Nested multipart/mixed](./22.5-Nested-Multipart)
6. [Request-level compression](./22.6-Request-Compressed)
7. [Part-level compression](./22.7-Part-Compressed)

## Gotchas

- `Add-KrFormRoute` injects `$FormPayload` directly into the runspace (you already have `$Context`).
- Content types: `Add-KrFormRoute` accepts `multipart/form-data` by default. The urlencoded and `multipart/mixed` chapters explicitly opt in via `KrFormOptions.AllowedRequestContentTypes`.
- For request-level compression examples, make sure you send a real `byte[]` body with `Content-Encoding: gzip`
(avoid returning an enumerated `Object[]` of bytes from helper functions).

## Multipart rules (`KrPartRule`)

These examples configure upload validation rules via `KrFormOptions.Rules`.

- Rules match by part **name** (the `name="..."` value from the `Content-Disposition` header).
- For `multipart/form-data`, this is usually present by default.
- For ordered `multipart/mixed`, you must include a `Content-Disposition` header with a `name=` for each part if you want rules to apply.
- If a part has no `name`, it can still be parsed (as ordered content), but no named rule can match it.

Common rule knobs used in this chapter:

- `Required`: Rejects the request if a named part is missing.
- `AllowMultiple`: Rejects multiple occurrences when set to `$false`.
- `AllowedContentTypes` / `AllowedExtensions`: Rejects mismatched uploads.
- `MaxBytes`: Rejects oversized parts.

## Prerequisites

- PowerShell 7.4+
- .NET runtime per repo `global.json`
