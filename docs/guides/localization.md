---
layout: default
parent: Guides
title: Localization
nav_order: 50
---

# Localization in Kestrun

This guide explains how request-localized string tables work in Kestrun, how to author
PowerShell `.psd1` string tables, configure the middleware, how culture resolution and
resource fallback operate, and how to access localized strings and formatting from
PowerShell runspaces and Razor pages.

## Key Concepts

- **Resources**: Place string tables under a per-culture folder inside a resources root
  (for example `Assets/i18n/en-US/strings.psd1`).
- **.psd1 format**: Use a PowerShell hashtable literal wrapped in `@{ ... }`. Values may
  be nested hashtables for grouping (e.g. `Labels = @{ Save = 'Save' }`). Quoted keys
  and values are supported.
- **Request culture vs resource culture**: Kestrun preserves the requested culture (used
  for formatting) while resolving which resource culture to use for string lookup (allows
  falling back to a related resource when an exact folder isn't present).
- **Injection**: `Context.Culture` and `Context.LocalizedStrings` (preferred) are available
  in PowerShell runspaces and Razor pages. `Context.Strings` remains an alias for
  compatibility. A `Localizer` variable is also injected for convenience.

## External reference (canonical culture tags)

- **BCP 47 (IETF language tags)**: Use standard tags like `en-US`, `fr-FR`, `it-CH`.
  See the canonical specification at <https://www.rfc-editor.org/info/bcp47> for guidance
  and tag syntax.

Getting started

- Create a resources folder in your example or app: `Assets/i18n/`
- Add one subfolder per culture you want to provide resources for, for example:

  - `Assets/i18n/en-US/strings.psd1`
  - `Assets/i18n/fr-FR/strings.psd1`
  - `Assets/i18n/it-IT/strings.psd1`

Sample `strings.psd1` (PowerShell hashtable)

```powershell
@{
    Hello = 'Hello'
    Labels = @{
        Save = 'Save'
        Cancel = 'Cancel'
    }
}
```

Middleware configuration

- Add the middleware and point it at your resources folder:

```powershell
Add-KrLocalizationMiddleware -ResourcesBasePath './Assets/i18n'
```

Culture resolution order (request-level)

- Query parameter: `?lang=<tag>` (highest precedence)
- Cookie named `lang`
- `Accept-Language` header
- Default server culture (fallback)

Resource culture resolution (string lookup)

- Kestrun resolves the resource culture separately from the requested culture:
  1. Look for an exact match folder (e.g. `it-CH`).
  2. If not found, try a language-specific sibling fallback (e.g. `it-IT`) when available.
  3. Fallback up the parent chain (e.g. `fr-CA` → `fr`), then default resources.

Notes on formatting and runspaces

- Kestrun sets `System.Globalization.CultureInfo.CurrentCulture` and `CurrentUICulture`
  inside route and Razor runspaces so standard formatting (dates, numbers, currency)
  respects the requested culture.
- The request-level culture is available as `Context.Culture` (string) and can be used to create a `CultureInfo` in scripts:

```powershell
$ci = [System.Globalization.CultureInfo]::new($Context.Culture)
$now.ToString('D', $ci)
1234.56.ToString('C', $ci)
[CultureInfo]::GetCultureInfo($Context.Culture).Calendar.GetType().Name
```

Accessing localized strings

- PowerShell: use `Get-KrLocalizedString -Key 'Labels.Save' -Default 'Save'` inside a route script or any injected runspace.
- Razor pages: `Context.LocalizedStrings['Hello']` or use the injected `Localizer` helper.

Authoring tips

- Prefer keys with grouping (e.g. `Labels.Save`) and nested hashtables in `.psd1` for maintainability.
- Keep culture folders minimal: put only strings that differ from the default; missing keys fall back to the next candidate culture.
- Avoid dotted keys with literal dots in the key names (use nested hashtables instead).

Troubleshooting

- If formatting seems wrong, confirm `Context.Culture` matches the requested tag and examine the runspace prelude that sets `CurrentCulture`.
- Use logging to inspect which resource folder was selected; the localization middleware also emits warnings when resource files are missing.  

See examples

- [Localization example script](../_includes/examples/pwsh/21.1-Localization.ps1)
- [Razor localization example](../_includes/examples/pwsh/21.2-Razor-Localization.ps1)

## Related pages

- Guide: [Localization Guide](../guides/localization.md)
