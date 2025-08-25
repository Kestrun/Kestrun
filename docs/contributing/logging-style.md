---
layout: default
title: Logging & Console Output
parent: Contributing
nav_order: 6
---

# Logging & Console Output Style

Consistent, scannable console output helps contributors quickly understand build and utility script progress.
This guide defines the canonical emoji/icon and wording conventions. Use these for `Write-Host` or future
`Write-KrLog` helper calls.

## Icon Map

| Category | Emoji | Usage Examples |
|----------|-------|----------------|
| Info / Generic | ℹ️ | General informational messages |
| Build / Compile | 🏗️ | Building solution / project |
| Restore / Fetch / Download | 📦 | Restoring packages, downloading tooling |
| Update / Sync | 🔄 | Syncing DLLs, updating analysis packages |
| Clean / Purge | 🧹 | Cleaning artifacts, coverage, docs |
| Test (start) | 🧪 | Starting test suites |
| Test (sub-step) | ▶️ | Per-framework test run indicator |
| Packaging / Archive | 📦 | Creating NuGet/package artifacts |
| Format | ✨ | Code formatting operations |
| Coverage (collect) | 📊 | Collecting or saving raw coverage XML |
| Coverage (report) | 📈 | Generating HTML / badges coverage reports |
| Documentation (aggregate) | 📚 | Generating all docs/help bundles |
| Documentation (PowerShell) | 📖 | PowerShell help generation |
| Documentation (C# API) | 📘 | C# XML → Markdown API docs |
| Install / Deploy | 🚀 | Deploying / installing module |
| External Tool Install | 📥 | Installing tools (xmldocmd, ReportGenerator) |
| Large File Generation | 🧪 | Creating synthetic test files |
| Copy / File Ops / Stage | 📁 | Copying/staging binaries, assets |
| Third-Party Notices | 📄 | Generating license / notice files |
| Manifest / Config | 📝 | Generating or updating config/manifest |
| Scan / Discovery | 🔍 | Scanning directories, locating files |
| Success / Final | ✅ | Successful completion summary |
| Warning | ⚠️ | Non-fatal issue / degraded path |
| Error / Failure | ❌ | Fatal error / abort condition |
| Removal / Deletion | 🗑️ | Deleting modules, temp, artifacts |

## Formatting Rules

1. Start each primary status line with the emoji then one space.
2. Use sentence case; prefer gerund verbs for ongoing work ("Building", "Generating").
3. Keep ellipses (`...`) only for clearly ongoing operations; omit otherwise.
4. One emoji per line; do not chain multiples.
5. Allow a blank line before a final ✅ success message if prior output is dense.
6. Leave streamed progress markers (e.g. `#`) untouched.
7. Do not substitute different but similar emojis; stay canonical.
8. For non-Unicode terminals, a future `-NoEmoji` or `KESTRUN_NO_EMOJI` suppression may be added.

## Before / After Example

Before:

```powershell
Write-Host 'Building solution...'
Write-Host 'Running tests for net9.0'
Write-Host 'Coverage (Cobertura) saved: X'
```

After:

```powershell
Write-Host '🏗️ Building solution...'
Write-Host '🧪 Running tests for net9.0'
Write-Host '📊 Coverage (Cobertura) saved: X'
```

## Suggested Helper (Future)

Centralize formatting and allow emoji suppression:

```powershell
function Write-KrLog {
    param(
        [Parameter(Mandatory)][string]$Message,
        [ValidateSet('info','build','restore','update','clean','test','package','format','coverage','report','docs','docs-pwsh','docs-cs','install','tool','generate','copy','notice','manifest','scan','success','warn','error','remove')]
        [string]$Kind = 'info',
        [switch]$NoEmoji
    )
    $map = @{ info='ℹ️'; build='🏗️'; restore='📦'; update='🔄'; clean='🧹'; test='🧪'; package='📦'; format='✨'; coverage='📊'; report='📈'; docs='📚'; 'docs-pwsh'='📖'; 'docs-cs'='📘'; install='🚀'; tool='📥'; generate='🧪'; copy='📁'; notice='📄'; manifest='📝'; scan='🔍'; success='✅'; warn='⚠️'; error='❌'; remove='🗑️' }
    $useEmoji = -not ($NoEmoji -or $env:KESTRUN_NO_EMOJI)
    $icon = if ($useEmoji) { $map[$Kind] } else { '' }
    if ($icon) { Write-Host "$icon $Message" } else { Write-Host $Message }
}
```

Adoption optional; recommended for future consistency.

## Migration Checklist

- [ ] Replace non-canonical or duplicate emojis
- [ ] Ensure gerund verb style for active operations
- [ ] Use ✅ for final success; ❌ for failures (plus `throw`)
- [ ] Leave streaming progress markers intact
- [ ] Avoid mixing packaging vs restore icons (both use 📦 intentionally)

---

Last updated: {{ site.time | date: '%Y-%m-%d' }}
