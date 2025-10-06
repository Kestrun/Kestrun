---
applyTo: "docs/pwsh/tutorial/**/*.md"
---
# Copilot Authoring Instructions: Tutorial Pages

These instructions formalize how Copilot should generate new tutorial documentation pages under `docs/pwsh/tutorial/*`.

## Core Directives (Strict)

1. Always create one markdown file per tutorial using the pattern `N.Title.md` (e.g., `6.Redirects.md`).
2. Place the file in the correct section folder (e.g., `docs/pwsh/tutorial/9.Responses/`).
3. Include YAML front matter with fields: `title`, `parent`, `nav_order`.
4. The first H1 (`#`) heading must exactly match the `title` value.
5. Provide a one-sentence purpose line immediately after the H1.
6. Include a `## Full source` section that references and includes the example script using an include directive.
7. Include a `## Step-by-step` numbered list describing the script actions (max 8 steps unless justification is documented).
8. Include a `## Try it` section with at least one `curl` example and (when relevant) PowerShell equivalents.
9. Include any feature-specific explanatory sections (e.g., `## Redirect Types`).
10. Provide a `## References` section linking all cmdlets used and any related topic docs (Health tutorials must link `/topics/health`).
11. Append a horizontal rule `---` followed by a `### Previous / Next` navigation block.
12. Add link reference definitions for the included script at the bottom.
13. Use consistent imperative style and avoid redundant commentary.
14. For Health probe examples, use `New-KrProbeResult` instead of direct constructors.
15. Maintain ordering exactly: Title → Intro → Full source → Step-by-step → Try it → Feature-specific blocks →
(Key Points / Troubleshooting optional) → References → --- → Previous / Next → link refs.
16. Do not omit `## Step-by-step`, `## Full source`, `## References`, or navigation unless explicitly instructed.

## Formatting Rules

- Code fences must specify language: `powershell`, `json`, `yaml`, `markdown` as appropriate.
- Step list: Each item begins with a capitalized noun or verb followed by a colon when labeling a component (e.g., `Logging:`) or plain description otherwise.
- Avoid lines longer than ~160 characters.
- Use backticks for inline cmdlets, routes, and file names.
- Avoid HTML tags—prefer plain markdown.

## Health-Specific Additions

When generating a Health tutorial:

- Ensure `## References` includes: endpoint cmdlets, probe cmdlets, `New-KrProbeResult`, and `[Health Topic](/topics/health)`.
- If showing sample JSON output, wrap in a fenced `json` block.
- Mention tag filtering, degraded status, or timeouts only if they appear in the sample code.

## Redirect / Response Pattern Notes

- Redirect examples must call `Write-KrRedirectResponse`.
- Always show at least one route mapping and server startup sequence.

## Standard Template (Fill Placeholders)

```markdown
---
title: TITLE
parent: SECTION_PARENT
nav_order: NUMBER
---

# TITLE

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

- Point 1
- Point 2

## References

- [Write-KrRedirectResponse](/pwsh/cmdlets/Write-KrRedirectResponse)
- [Health Topic](/topics/health) <!-- Include only if relevant -->

---

### Previous / Next

Previous: [PREV_TITLE](./PREV_FILE)
Next: [NEXT_TITLE](./NEXT_FILE)

```markdown

## Checklist (Automatable)

Copilot must verify before completing generation:

- [ ] Filename matches `N.Title.md` pattern.    
- [ ] Front matter present and valid.
- [ ] H1 matches title.
- [ ] Intro sentence present.
- [ ] `## Full source` includes correct script include path.
- [ ] Script reference link definition present at bottom.
- [ ] `## Step-by-step` present and numbered starting at 1.
- [ ] `## Try it` present with executable example.
- [ ] All used cmdlets listed in `## References`.
- [ ] Health page includes Health topic link (if applicable).
- [ ] Navigation block contains valid adjacent links (or `_None_`).
- [ ] No placeholder tokens (TITLE, SECTION_PARENT, NUMBER, etc.) remain.
- [ ] All code fences have language identifiers.

## Example: Redirects (Conforming)

See `docs/pwsh/tutorial/9.Responses/6.Redirects.md` for a live example of the pattern (add a `## Key Points` section if expanding).

## Maintenance

If conventions change, update BOTH this file and `tutorial-template.md`. Provide a summary of changes in commit messages referencing "Docs Pattern Update".
