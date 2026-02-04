---
title: File & Form Uploads
parent: Guides
nav_order: 165
---

# File & Form Uploads

Kestrun supports streaming form parsing for `multipart/form-data`, ordered multipart (`multipart/mixed`, etc.), and `application/x-www-form-urlencoded`,
with first-class support for **rules** and **limits**.

By default, `KrFormOptions.AllowedRequestContentTypes` allows only `multipart/form-data`.
To accept `application/x-www-form-urlencoded` or other `multipart/*` types, explicitly opt in via `AllowedRequestContentTypes`.

This guide focuses on how to configure:

- **Rules** (`KrPartRule`): what parts are allowed/required and how they’re validated.
- **Limits** (`KrFormLimits`): how much data is accepted and how parsing is bounded.
- **Annotations** (`KrBindForm` / `KrPart`): typed form models and OpenAPI-friendly request bodies.

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

## OpenAPI + annotation-based binding (PowerShell)

When you want **typed form binding** and **first-class OpenAPI request body generation**, use:

- `KrBindForm` on a PowerShell class to define form options/limits (and optionally select a template).
- `KrPart` on properties to define per-part rules (required, content types, max bytes, etc.).
- OpenAPI attributes (`OpenApiPath`, `OpenApiRequestBody`, `OpenApiSchemaComponent`, `OpenApiProperty`, …) to document the operation.

This approach is used across the upload tutorials (including the file-hash samples).

### Minimal example

```powershell
[OpenApiSchemaComponent(Description = 'File upload form', AdditionalPropertiesAllowed = $false)]
[KrBindForm(ComputeSha256 = $true, MaxRequestBodyBytes = 10MB, MaxPartBodyBytes = 10MB)]
class UploadModel {
    [KrPart(Required = $true, AllowMultiple = $false, ContentTypes = 'application/octet-stream', MaxBytes = 10MB)]
    [OpenApiProperty(Description = 'Binary file', Format = 'binary')]
    [Kestrun.Forms.KrFilePart] $file
}

function upload {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/upload')]
    [OpenApiResponse(StatusCode = '200', Description = 'OK', ContentType = 'application/json', Schema = [object])]
    param(
        [OpenApiRequestBody(ContentType = 'multipart/form-data', Required = $true)]
        [UploadModel] $FormPayload
    )

    $file = $FormPayload.file
    Write-KrJsonResponse @{ fileName = $file.OriginalFileName; bytes = $file.Length; sha256 = $file.Sha256 }
}
```

Notes:

- Part names come from the **property names** (`$FormPayload.file` binds to the multipart part named `file`).
- For file parts, use `[Kestrun.Forms.KrFilePart]` (or `[Kestrun.Forms.KrFilePart[]]`) to receive file metadata + `TempPath`.
- `ComputeSha256 = $true` populates `$file.Sha256` during parsing; for other hashes (MD5/SHA1/SHA384/SHA512), compute them from `$file.TempPath`.

### Templates (`KrBindForm.Template`)

If you already have a reusable `KrFormOptions` template (created via `Add-KrFormOption -Name 'MyTemplate' ...`), you can bind to it:

```powershell
[KrBindForm(Template = 'MyTemplate')]
class UploadModelFromTemplate {
    [KrPart(Required = $true)]
    [Kestrun.Forms.KrFilePart] $file
}
```

Important:

- When `Template` is set, Kestrun **clones the template** and uses it as-is for the route.
- In the current implementation, other `KrBindForm` properties are **not applied** when `Template` is set.

## `KrBindForm` attribute reference

`KrBindForm` declares **form parsing options + limits** for a model (or selects a template).

### Template selection

- `Template`: Name of a `KrFormOptions` template (from `Add-KrFormOption -Name ...`).
  - If set: the route uses a clone of that template.
  - If not set: a new options object is built from the other `KrBindForm` properties.

### Storage / hashing

- `DefaultUploadPath`: Overrides where file parts are stored (defaults to a temp `kestrun-uploads` folder).
- `ComputeSha256`: Computes SHA-256 during parse and populates `KrFilePart.Sha256`.

### Per-part decompression (multipart `Content-Encoding`)

- `EnablePartDecompression`: Enables decoding of `Content-Encoding` on individual parts.
- `AllowedPartContentEncodings`: Allowed encodings when decompression is enabled (defaults include `identity`, `gzip`, `deflate`, `br`).
- `RejectUnknownContentEncoding`: When `$true`, rejects parts using encodings not in the allow-list.
- `MaxDecompressedBytesPerPart`: Upper bound for the **decompressed** size of a single part.
  - Default is **20 MB**.
  - When decompression is enabled, the effective per-part cap becomes:
    - `min(MaxPartBodyBytes (or KrPart.MaxBytes), MaxDecompressedBytesPerPart)`
  - If you allow large uploads and decompression, raise this to match your expected maximum decompressed size.

### Request content-type enforcement

- `RejectUnknownRequestContentType`: When `$true` (default), rejects requests with content types not in `KrFormOptions.AllowedRequestContentTypes`.
  - `KrFormOptions.AllowedRequestContentTypes` defaults to `multipart/form-data`.
  - To accept other request content types (e.g. `application/x-www-form-urlencoded`, `multipart/mixed`), use a template via `Template`.

### Limits (same meaning as `KrFormLimits`)

- `MaxRequestBodyBytes`: Total request size cap.
- `MaxPartBodyBytes`: Default per-part cap (can be overridden per part using `KrPart.MaxBytes`).
- `MaxParts`: Maximum number of parts.
- `MaxHeaderBytesPerPart`: Maximum header bytes per part.
- `MaxFieldValueBytes`: Maximum size of a single field value.
- `MaxNestingDepth`: Maximum nested multipart depth (when parts contain `multipart/*`).

## `KrPart` attribute reference

`KrPart` defines a **rule** for a single named part. The part name is inferred from the **property name**.

- `Required`: When `$true`, rejects the request if the part is missing.
- `AllowMultiple`: When `$false`, rejects multiple parts with the same name.
- `ContentTypes`: Allowed content types for file parts (empty means “no restriction”).
- `Extensions`: Allowed file extensions (include the dot, e.g. `.txt`, `.json`).
- `MaxBytes`: Per-part max bytes (overrides `KrBindForm.MaxPartBodyBytes` / `KrFormLimits.MaxPartBodyBytes`).
- `StoreToDisk`: When `$true` (default), streams file parts to a temp file and sets `KrFilePart.TempPath`.
- `DestinationPath`: Optional per-part destination override (otherwise uses `DefaultUploadPath`).
- `Description`: Human-friendly text for rule diagnostics / metadata.
- `DecodeMode`: Scaffold placeholder (no enforcement/behavior guarantees yet in this branch).

## Payload model (`$FormPayload` / `KrFormPayload`)

Kestrun returns one of these payload shapes:

- **Named parts** (`multipart/form-data`, `application/x-www-form-urlencoded`)
  - `Fields`: map of `name -> string[]`
  - `Files`: map of `name -> KrFilePart[]`
- **Ordered parts** (`multipart/mixed` and other `multipart/*`)
  - `Parts`: ordered list of raw parts (optionally with a nested payload)

Note:

- Named parts and ordered multipart payloads are supported, but `Add-KrFormRoute` only accepts `multipart/form-data` by default.
Opt in to `application/x-www-form-urlencoded` and other `multipart/*` types via `KrFormOptions.AllowedRequestContentTypes`.

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

## Compression

Kestrun supports **request-level decompression middleware** (whole request body) and optional **per-part decompression** (individual multipart parts).
These are different mechanisms; enable the one that matches how your client sends data.

### Request-level decompression middleware (whole request `Content-Encoding`)

If a client compresses the **entire HTTP request body** (including the multipart boundaries), use request decompression middleware.

```powershell
# Allow gzip-compressed request bodies (Content-Encoding: gzip)
Add-KrRequestDecompressionMiddleware -AllowedEncoding gzip | Out-Null
```

Notes:

- This expects the *entire* request body to be compressed and the client to send `Content-Encoding: gzip`.
- This is independent of `KrFormOptions.EnablePartDecompression`.
- Limits still apply; for large uploads you typically raise `MaxRequestBodyBytes`/`MaxPartBodyBytes`/per-part max bytes.

Related tutorials:

- Request-level compression (rules): `/pwsh/tutorial/22.file-and-form-uploads/22.6-Request-Compressed`
- Request-level compression (OpenAPI): `/pwsh/tutorial/22.file-and-form-uploads/22.13-Request-Compressed-OpenAPI`

### Per-part decompression (multipart part `Content-Encoding`)

Kestrun can optionally decode *per-part* encodings (distinct from request-level decompression middleware):

- `KrFormOptions.EnablePartDecompression`
- `KrFormOptions.AllowedPartContentEncodings` (default includes `identity`, `gzip`, `deflate`, `br`)
- `KrFormOptions.RejectUnknownContentEncoding`
- `KrFormOptions.MaxDecompressedBytesPerPart`

Important:

- Prefer conservative `MaxDecompressedBytesPerPart` to avoid zip-bomb style attacks.
- If a client supplies `Content-Encoding` but part decompression is disabled, Kestrun can reject (`415`) when `RejectUnknownContentEncoding = true`.

Related tutorials:

- Part-level compression (rules): `/pwsh/tutorial/22.file-and-form-uploads/22.7-Part-Compressed`
- Part-level compression (OpenAPI): `/pwsh/tutorial/22.file-and-form-uploads/22.14-Part-Compressed-OpenAPI`

## Common pitfalls

- **413 Request body too large**: Increase `MaxRequestBodyBytes` / `MaxPartBodyBytes` and any per-part `MaxBytes` (`KrPart.MaxBytes` or `KrPartRule.MaxBytes`).
- **413 Decompressed part size exceeded (20MB default)**: If using per-part compression,
enable `EnablePartDecompression` and raise `MaxDecompressedBytesPerPart` to match expected decompressed size.
- **415 Unsupported Media Type**: Ensure the request `Content-Type` is allowed (default is `multipart/form-data`).
For `application/x-www-form-urlencoded` or `multipart/mixed`, opt in via a `KrFormOptions` template.
- **Template not applying overrides**: When `KrBindForm.Template` is set,
Kestrun clones the template and does not apply other `KrBindForm` overrides; put limits/content-types on the template.
- **Part name mismatch**: With `KrBindForm` models, the multipart part name must match the property name (e.g., property `file` expects `name="file"`).

## Operational guidance

- Prefer a dedicated upload directory per app instance (`DefaultUploadPath`), and regularly clean it.
- Turn on `ComputeSha256` only if you need hashing (it adds CPU cost).
- Start strict (rules + small limits), then loosen only what you need.

## References

- [Tutorial: File and Form Uploads](/pwsh/tutorial/22.file-and-form-uploads/)
- [Tutorial: File hash upload (OpenAPI)](/pwsh/tutorial/22.file-and-form-uploads/22.15-File-Hash-OpenAPI)
- [Tutorial: File hash upload (rules)](/pwsh/tutorial/22.file-and-form-uploads/22.16-File-Hash-Rule)
- [OpenAPI Guide](/guides/openapi)
- [Add-KrFormRoute](/pwsh/cmdlets/Add-KrFormRoute)
- [Add-KrFormOption](/pwsh/cmdlets/Add-KrFormOption)
- [New-KrFormPartRule](/pwsh/cmdlets/New-KrFormPartRule)
- [Add-KrRequestDecompressionMiddleware](/pwsh/cmdlets/Add-KrRequestDecompressionMiddleware)

External references:

- ASP.NET Core request decompression middleware: <https://learn.microsoft.com/aspnet/core/performance/request-decompression>
- RFC 7578 (multipart/form-data): <https://www.rfc-editor.org/rfc/rfc7578>
- RFC 9110 (HTTP semantics, Content-Encoding): <https://www.rfc-editor.org/rfc/rfc9110>
- RFC 1952 (gzip file format): <https://www.rfc-editor.org/rfc/rfc1952>
