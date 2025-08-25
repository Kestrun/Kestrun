---
layout: default
title: Troubleshooting
parent: Contributing
nav_order: 5
---

# Troubleshooting

Common issues and their resolutions when building or contributing.

## Build Fails Early

| Symptom | Cause | Fix |
|---------|-------|-----|
| `MSB` errors about SDK | Missing .NET 8 or 9 SDK | Install required SDKs; re-run `dotnet --list-sdks` |
| PowerShell module not found | Module path not imported | `Import-Module ./src/PowerShell/Kestrun/Kestrun.psm1` |
| Encoding issues | Non-UTF8 shell | Use a UTF-8 terminal / set `$OutputEncoding` |

## Tests Failing

| Symptom | Cause | Fix |
|---------|-------|-----|
| Pester not installed | Missing module | `Install-PSResource Pester -Scope CurrentUser` |
| Intermittent network test | External dependency | Remove external calls / mock them |
| Coverage file empty | No tests executed lines | Ensure routes/tests exercise code |

## Module Install Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Version folder exists | Reinstall without force | Add `-Force` support or delete folder manually |
| Commands not found | Module not on PSModulePath | Echo `$env:PSModulePath`; confirm install target |

## Docs Not Rendering

| Symptom | Cause | Fix |
|---------|-------|-----|
| Missing nav sidebar | Missing `parent` in front matter | Add `parent:` referencing correct section |
| Page out of order | `nav_order` collision | Adjust to unique integers |
| Liquid errors | Unescaped braces | Wrap code blocks properly |

## Icon / Emoji Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Boxes / tofu | Font lacks glyphs | Change terminal font (e.g. Cascadia Code PL) |
| Misaligned output | Wide emoji rendering | Consider future `-NoEmoji` switch |

## Git / PR Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Cannot push | Missing fork rights | Fork repo & add remote |
| Failing status checks | Tests / lint failing | Run `Invoke-Build Test` locally |
| Merge conflicts | Stale branch | Rebase onto `main` |

---
Last updated: {{ site.time | date: '%Y-%m-%d' }}
