# Kestrun Form Parsing Tutorials

This directory contains tutorial examples demonstrating the Kestrun form parsing subsystem.

## Overview

The Kestrun form parsing subsystem provides comprehensive support for parsing various form content types:

- **multipart/form-data** - File uploads with fields
- **application/x-www-form-urlencoded** - Traditional HTML forms
- **multipart/mixed** - Ordered parts (non-form-data)
- **Nested multipart** - One level of nested multipart sections
- **Request-level compression** - Gzip/deflate/brotli compressed requests

## Tutorials

### 22.1 - multipart/form-data File Upload
**File:** `22.1-Form-MultipartFormData.ps1`

Demonstrates file upload handling with:
- Multiple files per field
- Text fields alongside files
- SHA-256 hash computation
- Custom upload directory

**Key Features:**
- Streaming file storage (no memory buffering)
- Size limits per part
- Automatic filename sanitization

### 22.2 - application/x-www-form-urlencoded
**File:** `22.2-Form-UrlEncoded.ps1`

Shows traditional HTML form submission handling:
- Text fields
- Multiple values per field name
- Simple field extraction

### 22.3 - multipart/mixed Ordered Parts
**File:** `22.3-Form-MultipartMixed.ps1`

Handles ordered multipart sections:
- Parts preserve their sequence
- Parts may not have names
- Mixed content types in order

### 22.4 - Nested Multipart
**File:** `22.4-Form-NestedMultipart.ps1`

Demonstrates nested multipart parsing:
- One multipart section inside another
- Configurable nesting depth
- Nested payload access

### 22.5 - Request-Level Gzip Compression
**File:** `22.5-Form-RequestGzip.ps1`

Shows request-level compression handling:
- Content-Encoding: gzip/deflate/br
- Automatic decompression via middleware
- Works with multipart/form-data

## Common Patterns

### Basic Form Route

```powershell
$server | Add-KrFormRoute -Pattern '/upload' -ScriptBlock {
    $payload = $FormContext.Payload
    
    # Access fields
    $username = $payload.Fields['username'][0]
    
    # Access files
    foreach ($file in $payload.Files['files']) {
        Write-Host "Uploaded: $($file.OriginalFileName), Size: $($file.Length)"
    }
    
    Write-KrJsonResponse @{ success = $true }
}
```

### With Options

```powershell
$server | Add-KrFormRoute -Pattern '/upload' `
    -DefaultUploadPath './uploads' `
    -ComputeSha256 `
    -MaxPartBodyBytes 50MB `
    -MaxParts 20 `
    -ScriptBlock { ... }
```

### Request Decompression

```powershell
# Add BEFORE form routes
$server | Add-KrRequestDecompressionMiddleware

# Now form routes can handle Content-Encoding: gzip/deflate/br
$server | Add-KrFormRoute -Pattern '/upload' -ScriptBlock { ... }
```

## Payload Types

### Named Parts (KrNamedPartsPayload)
Used for `multipart/form-data` and `application/x-www-form-urlencoded`:

```powershell
$payload.Fields       # Dictionary<string, string[]>
$payload.Files        # Dictionary<string, KrFilePart[]>
```

### Ordered Parts (KrOrderedPartsPayload)
Used for `multipart/mixed` and other ordered multipart types:

```powershell
$payload.Parts        # List<KrRawPart>
```

Each `KrRawPart` may have:
- `Name` - Optional part name
- `ContentType` - MIME type
- `Length` - Size in bytes
- `TempPath` - Temporary file path
- `NestedPayload` - Nested multipart section (if any)

## File Management

### Uploaded File Structure (KrFilePart)

```powershell
$file.Name                # Field name
$file.OriginalFileName    # Original filename from client
$file.ContentType         # MIME type
$file.Length              # Size in bytes
$file.TempPath            # Temporary file path
$file.Sha256              # SHA-256 hash (if ComputeSha256 enabled)
$file.Headers             # Part headers
```

### Cleanup

Remember to clean up uploaded files after processing:

```powershell
foreach ($file in $payload.Files.Values | ForEach-Object { $_ }) {
    if (Test-Path $file.TempPath) {
        Remove-Item $file.TempPath
    }
}
```

Or move them to permanent storage:

```powershell
$permanentPath = Join-Path './permanent' $file.OriginalFileName
Move-Item $file.TempPath $permanentPath
```

## Security Considerations

### Size Limits

```powershell
Add-KrFormRoute -Pattern '/upload' `
    -MaxRequestBodyBytes 100MB `    # Total request size
    -MaxPartBodyBytes 50MB `         # Per-part size
    -MaxFieldValueBytes 1MB `        # Text field size
    -MaxParts 100                    # Maximum parts
```

### Decompression Bomb Protection

```powershell
Add-KrFormRoute -Pattern '/upload' `
    -EnablePartDecompression `           # Enable per-part decompression
    -MaxDecompressedBytesPerPart 100MB  # Limit decompressed size
```

### Filename Sanitization

Filenames are automatically sanitized using `Path.GetFileName()` with fallback to random GUID.

### Content Type Validation

Configure allowed content types in `KrFormOptions`:

```powershell
$options.AllowedRequestContentTypes = @(
    'multipart/form-data',
    'application/x-www-form-urlencoded'
)
$options.RejectUnknownRequestContentType = $true
```

## Testing with PowerShell

### multipart/form-data

```powershell
$multipart = [System.Net.Http.MultipartFormDataContent]::new()
$multipart.Add([System.Net.Http.StringContent]::new('value'), 'field')

$fileContent = [System.IO.File]::ReadAllBytes('./test.txt')
$content = [System.Net.Http.ByteArrayContent]::new($fileContent)
$multipart.Add($content, 'file', 'test.txt')

$client = [System.Net.Http.HttpClient]::new()
$response = $client.PostAsync('http://localhost:5000/upload', $multipart).Result
```

### application/x-www-form-urlencoded

```powershell
Invoke-RestMethod -Uri 'http://localhost:5000/form' -Method Post -Body @{
    username = 'john'
    email = 'john@example.com'
}
```

### With Gzip Compression

```powershell
# Requires System.IO.Compression
$body = "field1=value1&field2=value2"
$ms = [System.IO.MemoryStream]::new()
$gzip = [System.IO.Compression.GzipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress)
$sw = [System.IO.StreamWriter]::new($gzip)
$sw.Write($body)
$sw.Close()

$compressed = $ms.ToArray()
$ms.Dispose()

Invoke-RestMethod -Uri 'http://localhost:5000/form' -Method Post `
    -Body $compressed `
    -Headers @{ 'Content-Encoding' = 'gzip'; 'Content-Type' = 'application/x-www-form-urlencoded' }
```

## Performance Tips

1. **Streaming**: Files are streamed to disk, not loaded into memory
2. **Limits**: Set appropriate size limits to prevent abuse
3. **Hashing**: Only enable SHA-256 computation if needed
4. **Cleanup**: Clean up temp files promptly
5. **Compression**: Use request-level compression for large uploads

## Troubleshooting

### "Missing boundary parameter"
Ensure multipart Content-Type includes boundary:
```
Content-Type: multipart/form-data; boundary=----boundary123
```

### "Maximum size exceeded"
Increase the appropriate limit:
- `MaxRequestBodyBytes` - Total request
- `MaxPartBodyBytes` - Per part
- `MaxFieldValueBytes` - Text fields

### "Decompressed size limit exceeded"
Increase `MaxDecompressedBytesPerPart` or disable `EnablePartDecompression`

### Files not appearing in payload
Check:
- Content-Disposition has `filename` parameter
- Content-Type is `multipart/form-data`
- Part name matches expected field name

## Additional Resources

- [Kestrun Documentation](https://kestrun.io)
- [ASP.NET Core Request Decompression](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/request-decompression)
- [Multipart MIME RFC](https://www.rfc-editor.org/rfc/rfc2046)
