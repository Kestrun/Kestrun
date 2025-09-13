# Contributing to Kestrun

Thank you for bringing your brilliance to **Kestrun**. Whether you’re polishing docs, crafting elegant C#, or tuning PowerShell cmdlets,
you’re in the right place. 💫

---

## ✨ Ways to Contribute

- **Code**: features, bug fixes, performance improvements.
- **Docs**: tutorials, cmdlet help, architecture notes (must follow Just-the-Docs).
- **Tests**: increase coverage, add regression tests with Pester.
- **Issues/Discussion**: report bugs, propose ideas, share feedback.

---

## 🧰 Prerequisites

- **PowerShell 7.4 or greater**
- **.NET SDK** (8 or 9 recommended)
- **Invoke-Build** and **Pester** (installed via `Install-PSResource`)

Install the PowerShell build/test tooling:

```powershell
Install-PSResource -Name 'Invoke-Build','Pester' -Scope CurrentUser
```

> If you’re on a clean machine, ensure `Install-PSResource` is available (PowerShellGet v3).
> For module policy issues: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`.

---

## ▶️ Build & Test (the exact flow)

From the repository root:

### Restore & Build

```powershell
Invoke-Build Restore ; Invoke-Build Build
```

### Run Tests

```powershell
Invoke-Build Test
```

That’s the canonical pipeline used locally and by CI—keep it consistent.

---

## 🔧 Development Workflow

1. **Fork & branch**

   ```bash
   git checkout -b feature/my-delicious-change
   ```

2. **Code** (follow style guides below).
3. **Build & test**

   ```powershell
   Invoke-Build Restore ; Invoke-Build Build
   Invoke-Build Test
   ```

4. **Commit clearly**

   ```bash
   git commit -m "Add: KestrunHostManager supports multi-instance selection"
   ```

5. **Open a Pull Request** and fill out the PR template.

---

## 📝 Style & Quality

### C\#

- Follow Microsoft C# conventions.
- Prefer explicit types for public APIs; keep internals tidy.
- Use nullable reference types and `ConfigureAwait(false)` in library code where relevant.

### PowerShell

- Approved verbs (`Get-`, `New-`, `Add-`, `Set-`, `Remove-`, `Test-`, etc.).
- Include comment-based help for all public functions.
- Avoid global state; design for pipeline-friendliness.
- Keep cmdlets fast and predictable—pure where possible.

### Testing

- Prefer **Pester v5** tests colocated under `tests/`.
- One behavioral concern per test; name tests descriptively.
- When fixing a bug, add a failing test first.

---

## 📚 Documentation (Just-the-Docs compatible)

All docs must render cleanly with **[Just-the-Docs](https://github.com/just-the-docs/just-the-docs)** (as used by the Kestrun site).
Key rules:

- Every page requires a **front matter** block.
- Use **`parent`**, **`nav_order`**, and **`has_children`** to control navigation.
- Keep cmdlets under the **“PowerShell Cmdlets”** section; tutorials under **“Tutorials.”**

### Front Matter Templates

**Cmdlet page (example):**

```markdown
---
layout: default
parent: PowerShell Cmdlets
title: Get-KrScheduleReport
nav_order: 60
render_with_liquid: false
---

# Get-KrScheduleReport

> Short, imperative synopsis here.

## SYNOPSIS
Returns the full schedule report.

## SYNTAX

```powershell

Get-KrScheduleReport \[\[-Server] <KestrunHost>] \[\[-TimeZoneId] <String>] \[-AsHashtable]

````

## DESCRIPTION

Concise, user-focused description…

## EXAMPLES

```powershell
Get-KrScheduleReport -AsHashtable
````

## PARAMETERS

- **Server** — …
- **TimeZoneId** — …

````text

**Tutorial page (example):**
```markdown
---
layout: default
parent: Tutorials
title: Static Routes
nav_order: 3
---

# Introduction to Static Routes

A crisp overview…

## Quick start
```powershell
Invoke-Build Restore ; Invoke-Build Build
````

````text

### Navigation Tips (Just-the-Docs)
- Root landing page should be a friendly overview of features with deep links.
- Use `nav_order` to sort; lower numbers appear first.
- Use `has_children: true` on a section index page if it owns subpages.

**Section index example:**
```markdown
---
layout: default
title: PowerShell Cmdlets
nav_order: 30
has_children: true
---

# PowerShell Cmdlets

Browse the Kestrun command surface…
````

### Content Conventions

- **Headings**: Use `#`, `##`, `###` sensibly; keep titles short.
- **Callouts**: Use Markdown blockquotes:

  > **Note:** This behavior requires PowerShell 7.4+
  > **Warning:** Rotating secrets? Update appsettings too.
- **Code fences**: Use language hints (` ```powershell`, ` ```csharp`).
- **Links**: Relative links within the docs; absolute links for external sites.

---

## ✅ Pull Request Checklist

- [ ] Built successfully: `Invoke-Build Restore ; Invoke-Build Build`
- [ ] Tests pass: `Invoke-Build Test`
- [ ] New/changed behavior covered by Pester tests
- [ ] Public APIs documented (XML docs for C#, comment-based help for PowerShell)
- [ ] Docs are **Just-the-Docs** compliant and correctly placed (Cmdlets/Tutorials)
- [ ] Changelog entry if user-facing

---

## 🐛 Filing Issues

Please include:

- Repro steps and expected vs. actual behavior
- Versions: OS, PowerShell (must be 7.4+), .NET SDK
- Logs, stack traces, and minimal code samples

---

## 📜 License

By contributing, you agree your contributions are licensed under the [MIT License](LICENSE.txt).
