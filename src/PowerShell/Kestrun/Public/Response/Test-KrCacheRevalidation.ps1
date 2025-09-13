
<#
.SYNOPSIS
    Checks request validators and writes 304 if appropriate; otherwise sets ETag/Last-Modified.
.DESCRIPTION
    Returns $true if a 304 Not Modified was written (you should NOT write a body).
    Returns $false if cache missed; in that case the function sets validators on the response and
    you should write the fresh body/status yourself.
.PARAMETER Payload
    The response payload (string or byte[]) to hash for ETag generation.
    If you have a stable payload, use this to get automatic ETag generation.
    If you have a dynamic payload, consider using -ETag instead.
.PARAMETER ETag
    Explicit ETag token (quotes optional). If supplied, no hashing occurs.
.PARAMETER Weak
    Emit a weak ETag (W/"...").
.PARAMETER LastModified
    Optional last-modified timestamp to emit and validate.
.EXAMPLE
    if (-not (Test-KrCacheRevalidation -Payload $payload)) {
        Write-KrTextResponse -InputObject $payload -StatusCode 200
    } # writes auto-ETag based on payload
.EXAMPLE
    if (-not (Test-KrCacheRevalidation -ETag 'v1' -LastModified (Get-Date '2023-01-01'))) {
        Write-KrTextResponse -InputObject $payload -StatusCode 200
    } # writes explicit ETag and Last-Modified
#>
function Test-KrCacheRevalidation {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [object]$Payload,
        [Parameter()]
        [string]$ETag,
        [Parameter()]
        [switch]$Weak,
        [Parameter()]
        [DateTimeOffset]$LastModified
    )

    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        # Call the C# method on the $Context.Response object
        $handled = [Kestrun.Utilities.CacheRevalidation]::TryWrite304(
            $Context.HttpContext,
            $Payload,
            $ETag,
            $Weak.IsPresent,
            $LastModified
        )

        return $handled
    } else {
        Write-KrOutsideRouteWarning
    }
}
