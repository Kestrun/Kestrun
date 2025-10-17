---
title: Tutorial Page Authoring Template
parent: Contributing
nav_order: 99
render_with_liquid: false
---

# Tutorial Documentation Authoring Guide

This guide defines the structure, style, and content for new tutorial pages (e.g., `pwsh/tutorial/Section/6.Redirects.md`).
Use the template snippet below as a starting point and follow all mandatory sections.

## 1. File Naming & Numbering

- Location: `docs/pwsh/tutorial/<SectionFolder>/<N.TopicName>.md`
- `<N>` is the sequential number within that section (e.g., `6.Redirects.md`).
- Use `.` after the number, followed by a PascalCase or kebab-friendly title segment (no spaces). The on-page `#` Heading can contain spaces.
- Keep numbering contiguous; if you insert a page, adjust `nav_order` and filenames only if necessary.

## 2. Front Matter (Mandatory)

```yaml
---
title: Redirects            # Human-readable page title
parent: Responses           # Section folder title (must match existing parent page)
nav_order: 6                # Order inside the parent sidebar
---
```

Rules:

- `title` must exactly match the first `#` heading.
- `parent` must correspond to the higher-level index page.
- `nav_order` should align with filename numeric prefix.

## 3. Required Section Order

Every tutorial page SHOULD follow this order (omit optional sections only if empty):

1. `# <Title>` — Same as front matter title.
2. One-line purpose sentence (what the example demonstrates).
3. `## Full source` — Include the runnable script via an include.
4. `## Step-by-step` — Enumerated list describing each major action in the script.
5. `## Try it` — Minimal commands to exercise the feature (curl + PowerShell equivalents where relevant).
6. Feature-specific sections (e.g., `## Redirect Types`, `## Health Response Shape`) — Optional.
7. `## Key Points` — Optional bullet list summarizing important behaviors.
8. `## Troubleshooting` — Optional common pitfalls.
9. `## References` — Links to related cmdlets AND (when applicable) guide docs (e.g., Health: `/guides/health`).
10. Horizontal rule `---`.
11. `### Previous / Next

{: .fs-4 .fw-500}` — Two inline links to adjacent tutorials in the same section.
12. Footnote-style link references at bottom (for `Full source` file path and any others if preferred).

## 4. Full Source Section

Format:

```markdown
## Full source

File: [`pwsh/tutorial/examples/9.6-Redirects.ps1`][9.6-Redirects.ps1]

```powershell
{% include examples/pwsh/9.6-Redirects.ps1 %}
```

```markdown
- Always show the relative path to the example script under `docs/_includes/examples/pwsh/` (the include uses that path structure).

## 5. Step-by-step Rules

- Write numbered list; each item starts with a capital verb ("Logging:", "Server:").
- Health / Server samples convention:
  1. Logging registration (always Step 1 if present).
  2. Server creation.
  3. Listener binding.
  4. Runtime addition.
  5. Route / Probe mappings (in logical order).
  6. Configuration enable / server start.
- Keep each step single-purpose and concise.

## 6. Try It Section

Provide at least one `curl` command (or equivalent) reflecting the primary route. For PowerShell, prefer `Invoke-WebRequest` or `curl` (alias aware) showing headers or status. Use fenced `powershell` code blocks.

## 7. Feature-Specific Blocks

Add focused sections when extra patterns matter (e.g., different redirect styles, probe types, serialization formats). Use a short noun phrase heading.

## 8. References Section

Include:
- Each cmdlet used (e.g., `[Write-KrRedirectResponse](/pwsh/cmdlets/Write-KrRedirectResponse)`).
- Related guide doc(s): e.g., Health tutorials must link `[Health Guide](/guides/health)`.
- Keep bullet list—one link per line.

## 9. Previous / Next Navigation

Add after a horizontal rule `---`.

Format:

```markdown
---

### Previous / Next

{: .fs-4 .fw-500}

Previous: [Special Formats (BSON / CBOR)](./5.Special-Bson-Cbor)
Next: [Errors](./7.Errors)
```

- Use relative links (`./<AdjacentFileNameWithoutExt>`).
- If first or last in a section, write `Previous: _None_` or `Next: _None_`.

## 10. Link References (Optional)

Define included script path at bottom:

```markdown
[9.6-Redirects.ps1]: /pwsh/tutorial/examples/9.6-Redirects.ps1
```

Use this style for clarity when referenced multiple times; otherwise inline links are acceptable.

## 11. Code Style & Conventions

- Use backticks for cmdlets, routes, and file names inline.
- Prefer imperative present tense ("Send", "Return") for intros.
- Avoid repeating obvious comments already explained in Step-by-step.
- PowerShell examples: align hash table keys, avoid trailing semicolons, single quotes unless interpolation is required.
- Logging: Always show minimal logger setup if the output relies on logs.
- For health probe examples returning a custom status, use `New-KrProbeResult` wrapper instead of direct constructor calls.

## 12. Health Tutorial Specific Additions

When authoring a Health tutorial page:

- Ensure `## References` includes `[Health Guide](/guides/health)` plus all probe/endpoint cmdlets used.
- If showing response JSON, wrap in fenced `json` block.
- Mention tag filtering or status mapping only if directly relevant to the sample.

## 13. Accessibility & Clarity

- Keep line length reasonable (<110 chars preferred) for diff readability.
- Provide at most one conceptual table per file (placed early, usually in a quickstart or overview page, not in every tutorial).

## 14. Template Snippet

Copy & adapt:

```markdown
---
title: TITLE_HERE
parent: SECTION_PARENT
nav_order: NUMBER
---

# TITLE_HERE

One sentence purpose statement.

## Full source

File: [`pwsh/tutorial/examples/SECTION.NUMBER-slug.ps1`][EXAMPLE_REF.ps1]

```powershell
{% include examples/pwsh/SECTION.NUMBER-slug.ps1 %}
```

## Step-by-step

1. Logging: Register console logger as default.
2. Server: Create server named 'SECTION.NUMBER'.
3. Listener: Listen on 127.0.0.1:5000.
4. Runtime: Add PowerShell runtime.
5. Route: Describe primary route behavior.
6. Enable configuration and start server.

## Try it

```powershell
curl -i http://127.0.0.1:5000/route
```

## Key Points

- Point 1.
- Point 2.

## References

- [Write-KrRedirectResponse](/pwsh/cmdlets/Write-KrRedirectResponse)
- [Health Guide](/guides/health) <!-- Include when relevant -->

---

### Previous / Next

{: .fs-4 .fw-500}

Previous: [PREV_TITLE](./PREV_FILE)
Next: [NEXT_TITLE](./NEXT_FILE)

```markdown

## 15. Review Checklist (Before Commit)

- [ ] Front matter valid (title/parent/nav_order).
- [ ] All required sections present & ordered.
- [ ] Step numbering starts at 1 and flows logically.
- [ ] References include all cmdlets used.
- [ ] Previous/Next links resolve to existing files.
- [ ] Example include path matches actual script.
- [ ] Health pages: include Health topic link.
- [ ] No stray tabs or trailing spaces.

---

Following this guide ensures consistency across tutorial pages and reduces future refactoring work. Keep this document updated if conventions evolve.
