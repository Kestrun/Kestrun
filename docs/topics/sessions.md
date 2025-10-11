---
title: Sessions
nav_order: 70
---

# Sessions

Sessions provide per-client, cookie-based state across multiple HTTP requests.
Use them for small, transient data like counters, selected items,
or the current user identity after login.

## Overview

- Backed by an `IDistributedCache` (in-memory by default) with a session cookie key.
- Suitable for modest data (a few KB). Avoid storing large payloads; prefer databases or distributed caches directly.
- Security: Configure the cookie as `Secure`, `HttpOnly`, and an appropriate `SameSite` value.

## Enabling Sessions

```powershell
# Cookie builder (recommended)
$cookie = New-KrCookieBuilder -Name 'Kr.Session' -HttpOnly -SameSite Lax -SecurePolicy Always

# Register services + middleware, using in-memory cache by default
Add-KrSession -Cookie $cookie -IdleTimeout 20 -IOTimeout 10
```

> If you are wiring Redis/SQL yourself, add your cache provider first and use `-NoDistributedMemoryCache`.

## Usage Patterns

```powershell
# Counter (int)
$n = Get-KrSessionInt32 -Key 'counter'
Set-KrSessionInt32 -Key 'counter' -Value ($n + 1)

# Identity (string)
$user = Get-KrRequestQuery -Name 'user'
if ($user) { Set-KrSessionString -Key 'user' -Value $user }

# Retrieve
$who = Get-KrSessionString -Key 'user'

# Clear session
Clear-KrSession
```

## Cookie and Transport

- Set `SecurePolicy Always` to restrict cookies to HTTPS. For local HTTP testing, change to `None`.
- `SameSite Lax` is a safe default for most apps; set `Strict` for tighter CSRF posture or `None` for cross-site flows with HTTPS.

## Timeouts

- `IdleTimeout`: Inactivity window after which the session is abandoned (e.g., 20s in samples).
- `IOTimeout`: Maximum time allowed for reading/writing a session store.

## Testing Tips

- Use a single client with a cookie jar to keep the session: `curl -c jar -b jar https://...`.
- Start a fresh session by clearing the jar or using a new WebRequestSession in PowerShell.

## Example

- Tutorial sample: [Sessions middleware and routes](/pwsh/tutorial/10.middleware/5.Sessions)

## References

- [Add-KrSession](/pwsh/cmdlets/Add-KrSession)
- [New-KrCookieBuilder](/pwsh/cmdlets/New-KrCookieBuilder)
- [Get-KrSessionInt32](/pwsh/cmdlets/Get-KrSessionInt32)
- [Set-KrSessionInt32](/pwsh/cmdlets/Set-KrSessionInt32)
- [Get-KrSessionString](/pwsh/cmdlets/Get-KrSessionString)
- [Set-KrSessionString](/pwsh/cmdlets/Set-KrSessionString)
- [Clear-KrSession](/pwsh/cmdlets/Clear-KrSession)
