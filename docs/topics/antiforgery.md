---
title: Antiforgery (CSRF) Protection
parent: Guides
nav_order: 50
---

# Antiforgery (CSRF) Deep Dive

Cross-Site Request Forgery (CSRF) protection in Kestrun builds on ASP.NET Core's antiforgery services and exposes
clean PowerShell cmdlets:

- `Add-KrAntiforgeryMiddleware`
- `Add-KrAntiforgeryTokenRoute`
- Per-route opt out via `MapRouteOptions.DisableAntiforgery`

Use this guide for architecture details, configuration matrix, SPA / form integration patterns, and troubleshooting.

> For a quick hands-on tutorial, see: [Tutorial – Antiforgery Protection](/pwsh/tutorial/10.middleware/1.Antiforgery)

## 1. Threat Model

A CSRF attack tricks a victim's browser (already authenticated via cookies) into sending a state-changing request the user did not intend.
The defense: pair a *session-bound secret* (cookie) with a *request-bound assertion* (header/form field) that an attacker cannot supply off-site.

## 2. Token Architecture

| Component | Description | Default / Sample Value |
|-----------|-------------|-------------------------|
| Cookie    | Stores session-scoped token; HttpOnly; SameSite=Lax; Secure | `.Kestrun.AntiXSRF` |
| Request Token | Derived token validated against cookie context | Returned by `/csrf-token` |
| Header Name | Where clients echo the token for unsafe verbs | `X-CSRF-TOKEN` |
| Form Field (optional) | Legacy form POST field name | (Not set by default) |
| Token Endpoint | Issues JSON `{ token, headerName }` payload | `/csrf-token` |

### Lifecycle

1. Client loads any page OR explicitly calls `/csrf-token`.
2. Server sets antiforgery cookie + returns token JSON.
3. Client stores token in JS memory (never persistent storage if possible).
4. For unsafe verbs (POST/PUT/PATCH/DELETE) client sends both cookie + header.
5. Middleware validates; on success request continues; on failure returns error response.

## 3. Configuration Matrix

| Scenario | CookieName | HeaderName | SameSite | SecurePolicy | Notes |
|----------|------------|-----------|----------|--------------|-------|
| Default Dev | `.Kestrun.AntiXSRF` | `X-CSRF-TOKEN` | Lax | Always | Works for typical SPA navigation |
| Strict Isolation | Custom | `X-CSRF-TOKEN` | Strict | Always | No cross-site top-level navigations using cookie |
| Legacy Forms | `.Kestrun.AntiXSRF` | (None) | Lax | Always | Use `-FormFieldName '__RequestVerificationToken'` |
| API Only (Bearer) | (Disable) | (Disable) | N/A | N/A | Skip antiforgery; rely on Authorization header |
| Framed App (iFrame) | `.Kestrun.AntiXSRF` | `X-CSRF-TOKEN` | Lax | Always | Might need to allow framing + consider clickjacking mitigations |

## 4. Cmdlet Reference Patterns

### Add Middleware

```powershell
Add-KrAntiforgeryMiddleware -CookieName '.Kestrun.AntiXSRF' -HeaderName 'X-CSRF-TOKEN'
```

### Add Token Route

```powershell
Add-KrAntiforgeryTokenRoute -Path '/csrf-token' | Out-Null
```

### Disable For One Route

```powershell
$options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
$options.Pattern = '/public-status'
$options.HttpVerbs = [Kestrun.Utilities.HttpVerb[]] @('get')
$options.Code = { Write-KrJsonResponse @{ ok = $true } }
$options.DisableAntiforgery = $true
Add-KrMapRoute -Options $options
```

## 5. SPA Integration (Fetch / Axios)

Typical flow:

```javascript
async function getCsrfToken() {
  const r = await fetch('/csrf-token', { credentials: 'include' });
  const data = await r.json();
  return data.token; // cache in memory
}

async function postProfile(profile) {
  const token = await getCsrfToken();
  const r = await fetch('/profile', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    body: JSON.stringify(profile)
  });
  return r.json();
}
```

## 6. HTML Form Integration

If you want a hidden form field instead of a header:

```powershell
Add-KrAntiforgeryMiddleware -CookieName '.Kestrun.AntiXSRF' -FormFieldName '__RequestVerificationToken'
Add-KrAntiforgeryTokenRoute -Path '/csrf-token' | Out-Null
```

Then inject into form (server-side render or JS fetch):

```html
<form method="post" action="/profile">
  <input type="hidden" name="__RequestVerificationToken" value="TOKEN_FROM_ENDPOINT" />
  <input type="text" name="name" />
  <button type="submit">Save</button>
</form>
```

## 7. Error Responses

When validation fails the middleware returns an error (status 400/403 depending on policy). Log correlation IDs to tie user reports to server diagnostics.

## 8. Security Considerations

| Risk | Mitigation |
|------|------------|
| Token Theft via XSS | Use HttpOnly cookie; never persist header token; fix XSS rapidly |
| Clickjacking | Combine with `X-Frame-Options` / CSP frame-ancestors |
| Mixed Content | Always serve over HTTPS (`Secure` cookie) |
| Over-broad Opt-out | Limit `DisableAntiforgery` to true GET-only diagnostics endpoints |
| Token Replay Across Users | Token cryptographically bound to cookie; do not share cookies |

### 8.1 Multi-Verb Routes Behavior (New)

When you map a single route to multiple HTTP verbs (for example `GET` + `POST`) the antiforgery
validation now only runs for the **actual incoming unsafe verb** of the request.

Unsafe verbs (state changing): `POST`, `PUT`, `PATCH`, `DELETE`

Safe verbs (never force a CSRF token by default): `GET`, `HEAD`, `OPTIONS`, `TRACE`

Implications:

1. A combined `GET/POST` endpoint will not 400 a plain GET request just because POST is also configured.
2. If the route supports `POST` but the request is `GET`, no token is required. This is helpful for form
  bootstrap endpoints that also handle their own submission.
3. If you disable antiforgery (`DisableAntiforgery = true`) the route skips validation for all verbs.
  Use sparingly; prefer restricting to read-only endpoints.
4. If a client sends an unsafe verb that was **not** configured for the route, normal 404 / 405 semantics
  apply before antiforgery logic runs.

Rationale:

This reduces unnecessary 400 responses on mixed routes and aligns validation strictly with
state‑changing operations. It mirrors the browser threat model: CSRF risk exists when the browser can be
coerced into a write action.

Testing Tip:

If you previously had a test expecting a 400 on a GET to a mixed route, update it to expect `200 OK`
(or whatever the route returns) unless you explicitly forced validation.

Migration Checklist:

- [ ] Remove client token header injections on pure GET requests to mixed routes (no longer required).
- [ ] Confirm POST / PUT / PATCH / DELETE still include both cookie + header.
- [ ] Audit custom middleware that assumed per-route unconditional validation; make it method-aware.

Example (PowerShell):

```powershell
$opts = [Kestrun.Hosting.Options.MapRouteOptions]::new()
$opts.Pattern = '/profile'
$opts.HttpVerbs = [Kestrun.Utilities.HttpVerb[]] @('get','post')
$opts.Code = {
  param($Context)
  if($Context.Request.Method -eq 'GET') { Write-KrTextResponse 'PROFILE FORM' } else { Write-KrJsonResponse @{ saved = $true } }
}
Add-KrMapRoute -Options $opts
```

Now:

- `GET /profile` → does NOT require token
- `POST /profile` → MUST send token header + cookie

This behavior is automatic; no extra configuration needed.

## 9. Troubleshooting

| Symptom | Likely Cause | Resolution |
|---------|--------------|-----------|
| 400 Missing token | Header not sent | Ensure JS client adds header for unsafe verbs |
| 400 Token/cookie mismatch | Stale token after cookie rotation | Re-fetch token before POST; do not cache long term |
| Works in local, fails in prod | Domain / path mismatch | Inspect cookie attributes in browser dev tools |
| Token endpoint 404 | Route not added | Call `Add-KrAntiforgeryTokenRoute` prior to start |
| Form posts failing | Using header but middleware configured for form field only | Supply form field or configure header name |

## 9.1 Optional Form Body Suppression (NET 9+)

Starting with .NET 9, ASP.NET Core's antiforgery system added an option to skip reading the
antiforgery token from the request form body (`SuppressReadingTokenFromFormBody`). Kestrun
surfaces this via the same underlying `AntiforgeryOptions` and gates it by target framework.

Why suppress form body reading?

- Performance: Avoid buffering / parsing large multipart or form bodies when you only use the header token path.
- Clarity: Enforces a single token transport (header) for SPAs.
- Security Hygiene: Reduces accidental acceptance of stale hidden fields in mixed clients.

How to detect support at runtime:

```powershell
Test-KrCapability -Feature 'SuppressReadingTokenFromFormBody'
```

Or enumerate all features:

```powershell
Get-KrFeatureSupport -Capabilities | Where-Object Supported
```

Enabling (PowerShell):

```powershell
if (Test-KrCapability -Feature 'SuppressReadingTokenFromFormBody') {
  Add-KrAntiforgeryMiddleware -HeaderName 'X-CSRF-TOKEN' -SuppressReadingTokenFromFormBody | Out-Null
} else {
  Add-KrAntiforgeryMiddleware -HeaderName 'X-CSRF-TOKEN' | Out-Null
}
```

Enabling (C# host builder):

```csharp
host.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
#if NET9_0_OR_GREATER
    o.SuppressReadingTokenFromFormBody = true;
#endif
});
```

Behavioral change when enabled:

- Hidden form field values (e.g. `__RequestVerificationToken`) are ignored.
- Only the header token path is evaluated.
- Token endpoint exposure still returns the token; client must always echo it in the header for unsafe verbs.

Migration guidance:

| Scenario | Keep Form Field? | Suggested Setting |
|----------|------------------|-------------------|
| SPA / Fetch only | No | Suppress (set true) |
| Mixed (Razor + JS) | Maybe | Leave false (default) |
| Legacy pure forms | Yes | Leave false |

If you turn this on in an app that previously relied on form posts, ensure the JavaScript layer injects the header.

Capability Gate Reference:

- Feature name: `SuppressReadingTokenFromFormBody`
- Minimum TFM: .NET 9.0
- API surface: `AntiforgeryOptions.SuppressReadingTokenFromFormBody` (when compiled for net9+)
- Kestrun detection: `Test-KrCapability -Feature 'SuppressReadingTokenFromFormBody'`

## 10. Related Docs

- Tutorial: [Antiforgery Protection](/pwsh/tutorial/10.middleware/1.Antiforgery)
- Cmdlet: [Add-KrAntiforgeryMiddleware](/pwsh/cmdlets/Add-KrAntiforgeryMiddleware)
- Cmdlet: [Add-KrAntiforgeryTokenRoute](/pwsh/cmdlets/Add-KrAntiforgeryTokenRoute)
- OWASP CSRF: <https://owasp.org/www-community/attacks/csrf>

## 11. Further Reading (External)

Broader context, standards, and deep dives that inform antiforgery design:

- OWASP CSRF Prevention Cheat Sheet: <https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html>
- OWASP Top 10 (A01 Broken Access / A05 Security Misconfiguration relevance): <https://owasp.org/www-project-top-ten/>
- MDN: SameSite cookies explained: <https://developer.mozilla.org/docs/Web/HTTP/Headers/Set-Cookie/SameSite>
- MDN: Fetch credentials / cookies guidance: <https://developer.mozilla.org/docs/Web/API/fetch#credentials>
- Microsoft Docs (ASP.NET Core Antiforgery Service): <https://learn.microsoft.com/aspnet/core/security/antiforgery>
- Axios XSRF documentation (header conventions): <https://axios-http.com/docs/req_config>
- RFC 6454 – The Web Origin Concept (origin boundary): <https://datatracker.ietf.org/doc/html/rfc6454>
- Draft (Cookies: HTTP State Management Mechanism – current bis): <https://datatracker.ietf.org/doc/draft-ietf-httpbis-rfc6265bis/>
- Content Security Policy (reducing XSS -> protects tokens): <https://developer.mozilla.org/docs/Web/HTTP/CSP>

When evaluating alternatives (e.g., double-submit cookie vs synchronizer token vs SameSite=strict posture)
weigh usability (SPAs, OAuth redirects) against attack surface.

For high-risk apps, combine:

- Antiforgery tokens
- SameSite=Lax or Strict (balanced with legitimate cross-site redirects)
- Origin / Referer header verification for unsafe verbs
- Strong CSP to minimize XSS that could harvest tokens

Return to the [Guides index](./index) or the [Tutorial index](/pwsh/tutorial/index).
