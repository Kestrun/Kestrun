---
title: File & Form Uploads
parent: Guides
nav_order: 165
---

# File & Form Uploads

Kestrun supports streaming form parsing for `multipart/form-data`, ordered multipart (`multipart/mixed`, etc.), and `application/x-www-form-urlencoded`,
with first-class support for **rules** and **limits**.

By default, `KrFormOptions.AllowedRequestContentTypes` allows only `multipart/form-data`. To accept `application/x-www-form-urlencoded` or other `multipart/*` types, explicitly opt in via `AllowedRequestContentTypes`.

This guide focuses on how to configure:

- **Rules** (`KrPartRule`): what parts are allowed/required and how they’re validated.
- **Limits** (`KrFormLimits`): how much data is accepted and how parsing is bounded.

## Quick start (PowerShell)

Use `Add-KrFormRoute` for POST endpoints that should parse forms:

```powershell
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads'
$options.ComputeSha256 = $true

# Rule: require exactly one text/plain file named "file"
$fileRule = [Kestrun.Forms.KrPartRule]::new()
$fileRule.Name = 'file'
$fileRule.Required = $true
$fileRule.AllowMultiple = $false
$fileRule.AllowedContentTypes.Add('text/plain')
$options.Rules.Add($fileRule)

Add-KrFormRoute -Pattern '/upload' -Options $options -ScriptBlock {
    $file = $FormPayload.Files['file'][0]
    Write-KrJsonResponse @{ fileName = $file.OriginalFileName; bytes = $file.Length; sha256 = $file.Sha256 }
}
```

Notes:

- `Add-KrFormRoute` injects the parsed payload as `$FormPayload` (you still have `$Context`).
- Content types: `Add-KrFormRoute` defaults to `multipart/form-data` only; opt in to other request content types via `KrFormOptions.AllowedRequestContentTypes`.
- When a rule/limit is violated, the route returns a non-200 status (commonly `400`, `413`, `415`) with a short text message.

## Quick start (C#)

If you map form routes in C#, parsing is performed before your handler runs:

```csharp
app.MapPost("/upload", async httpContext =>
{
    var options = new KrFormOptions
    {
        DefaultUploadPath = Path.Combine(Path.GetTempPath(), "kestrun-uploads"),
        ComputeSha256 = true,
    };

    var payload = await KrFormParser.ParseAsync(httpContext, options, httpContext.RequestAborted);

    var file = payload is KrNamedPartsPayload named ? named.Files["file"][0] : null;
    return Results.Ok(new { file?.OriginalFileName, file?.Length, file?.Sha256 });
});
```

## Payload model (`$FormPayload` / `KrFormPayload`)

Kestrun returns one of these payload shapes:

- **Named parts** (`multipart/form-data`, `application/x-www-form-urlencoded`)
  - `Fields`: map of `name -> string[]`
  - `Files`: map of `name -> KrFilePart[]`
- **Ordered parts** (`multipart/mixed` and other `multipart/*`)
  - `Parts`: ordered list of raw parts (optionally with a nested payload)

Note:

- Named parts and ordered multipart payloads are supported, but `Add-KrFormRoute` only accepts `multipart/form-data` by default. Opt in to `application/x-www-form-urlencoded` and other `multipart/*` types via `KrFormOptions.AllowedRequestContentTypes`.

Important:

- **Rules (`KrPartRule`) match by name.** For ordered multipart, a part usually has no name unless you include a `Content-Disposition` header with `name="..."`.

## Rules (`KrPartRule`) – deep dive

Rules let you enforce what the client is allowed to send. They apply per-part and are matched by the part name.

### Rule matching

- `KrPartRule.Name` matches the parsed part name (from `Content-Disposition: ...; name="..."`).
- Matching is case-insensitive.
- If there is no name, a named rule cannot match that part.

### Option reference

#### `Name` (required)

The part name the rule applies to.

- For file upload fields, this is typically the HTML `<input name="file" type="file">` name.
- For text fields, this is the HTML `<input name="note" ...>` name.

#### `Required`

When `true`, the request is rejected if the named part is missing.

Typical status: `400 Bad Request`.

#### `AllowMultiple`

Controls whether multiple file parts with the same name are allowed.

- `AllowMultiple = $false`: rejects a second file with that name.
- `AllowMultiple = $true`: accepts multiple files with the same name (useful for “multi-select uploads”).

Typical status: `400 Bad Request`.

#### `AllowedContentTypes`

Restricts the allowed content types for a **file part**.

- If the list is empty, no restriction is applied.
- If non-empty, the file part’s content type must match one of the allowed values.

Typical status: `415 Unsupported Media Type`.

#### `AllowedExtensions`

Restricts allowed filename extensions for a **file part**.

- Extensions are compared case-insensitively.
- Include the dot (e.g., `.txt`, `.json`).

Typical status: `400 Bad Request`.

#### `MaxBytes`

Maximum bytes for this part.

- If set, it overrides the per-part default (`KrFormLimits.MaxPartBodyBytes`) for this part.
- Use it to allow large uploads for a specific part or to constrain a risky part more tightly.

Typical status on limit exceed: `413 Payload Too Large`.

#### `StoreToDisk`

Controls whether Kestrun stores this part on disk.

- `true` (default): part is streamed to a temp file under the chosen destination.
- `false`: Kestrun drains the stream and records only metadata (no file on disk).

Use `false` for cases where you only want to validate that something was uploaded (or you implement your own streaming sink).

#### `DestinationPath`

Overrides where the part is written when `StoreToDisk = true`.

- If not set, Kestrun uses `KrFormOptions.DefaultUploadPath`.
- Recommended: point to a temp folder or a dedicated upload directory, not your repo.

#### `DecodeMode`

Currently a scaffold/placeholder in this branch (`KrPartDecodeMode`).

- Use it only for documentation/forward-compat; enforcement/decoding behavior is not yet implemented.

## Limits (`KrFormLimits`) – deep dive

Limits ensure parsing is bounded and protects the server from abuse.

These are configured via `KrFormOptions.Limits`.

### Limits option reference

#### `MaxRequestBodyBytes`

Maximum total request body size.

- Applied at the ASP.NET Core request-body limit feature when available.
- If exceeded, the request is rejected.

Typical status: `413 Payload Too Large`.

#### `MaxPartBodyBytes`

Maximum bytes per part (default per-part limit).

- Applies to both file parts and ordered raw parts.
- Can be overridden per part with `KrPartRule.MaxBytes`.

Typical status: `413 Payload Too Large`.

#### `MaxParts`

Maximum number of multipart sections processed.

Typical status: `413 Payload Too Large`.

#### `MaxHeaderBytesPerPart`

Maximum bytes for headers in a single part.

Use this to defend against oversized `Content-Disposition` / custom headers.

Typical status: `413 Payload Too Large` or `400 Bad Request` depending on where parsing fails.

#### `MaxFieldValueBytes`

Maximum bytes for a single field value.

Typical status: `413 Payload Too Large`.

#### `MaxNestingDepth`

Maximum nested multipart depth (for nested `multipart/*` inside ordered multipart bodies).

Typical status: `413 Payload Too Large`.

## Part decompression (multipart `Content-Encoding`)

Kestrun can optionally decode *per-part* encodings (distinct from request-level decompression middleware):

- `KrFormOptions.EnablePartDecompression`
- `KrFormOptions.AllowedPartContentEncodings` (default includes `identity`, `gzip`, `deflate`, `br`)
- `KrFormOptions.RejectUnknownContentEncoding`
- `KrFormOptions.MaxDecompressedBytesPerPart`

Important:

- Prefer conservative `MaxDecompressedBytesPerPart` to avoid zip-bomb style attacks.
- If a client supplies `Content-Encoding` but part decompression is disabled, Kestrun can reject (`415`) when `RejectUnknownContentEncoding = true`.

## Operational guidance

- Prefer a dedicated upload directory per app instance (`DefaultUploadPath`), and regularly clean it.
- Turn on `ComputeSha256` only if you need hashing (it adds CPU cost).
- Start strict (rules + small limits), then loosen only what you need.

## References

- [Tutorial: File and Form Uploads](/pwsh/tutorial/22.file-and-form-uploads/)
- [Add-KrFormRoute](/pwsh/cmdlets/Add-KrFormRoute)
- [Add-KrRequestDecompressionMiddleware](/pwsh/cmdlets/Add-KrRequestDecompressionMiddleware)
