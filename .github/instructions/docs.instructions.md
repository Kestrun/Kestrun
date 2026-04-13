---
applyTo: "docs/**,**/*.md"
---

# Documentation Instructions

Documentation changes must follow `.github/copilot-instructions.md` as the global source of truth.
These rules apply when creating or editing documentation files.

## Core Standards

- Do not invent APIs, cmdlets, routes, options, defaults, or behavior.
- Verify factual statements against repository sources such as C# code, PowerShell module code, tests, example scripts, or existing guides.
- Prefer concise, technical explanations over marketing language.
- Use repository terminology consistently. Prefer the names used in code, tests, and cmdlet help.
- Preserve existing frontmatter, headings, includes, and page structure unless a documentation refactor is explicitly requested.
- Prefer small, incremental edits that are easy to review.
- Update affected cross-references, adjacent navigation links, and example file references when modifying docs.
- If a detail cannot be verified from source, say so or omit it. Prefer "unknown" over guessing.
- Do not modify production code while performing documentation-only tasks unless explicitly requested.

## Verification Workflow

1. Read the target documentation file before editing.
2. Inspect the related implementation, tests, examples, or generated output referenced by that doc.
3. Identify only the inaccuracies, omissions, or stale references needed for the requested change.
4. Apply the smallest precise edit that resolves the issue.
5. Re-check nearby links, headings, and terminology for consistency with adjacent documentation.

## Repository-Specific Guidance

- For PowerShell tutorial pages under `docs/pwsh/tutorial/`, follow the structure and ordering defined in `docs/contributing/tutorial-template.md`.
- Keep tutorial pages aligned with the example scripts they embed or reference.
- When updating cmdlet or guide links, prefer existing repository link patterns such as `/pwsh/cmdlets/<Cmdlet-Name>` and `/guides/<guide-name>`.
- Preserve Liquid includes and frontmatter fields used by the docs site unless the task explicitly requires changing them.
- When a tutorial page is changed, verify `Previous` and `Next` navigation links still point to the correct adjacent pages.
- If tutorial conventions change, keep `docs/contributing/tutorial-template.md` consistent with the updated convention.

## Style Expectations

- Use Markdown headings that match the surrounding file’s structure.
- Use fenced code blocks with language identifiers when adding or editing examples.
- Keep prose direct and implementation-focused.
- Use backticks for cmdlets, routes, filenames, and literal values.
- Avoid placeholder text, speculative guidance, and unsupported recommendations.

## Completion Notes

When reporting documentation work, explain:

- what documentation was updated
- what repository sources were used to verify the change
- any remaining uncertainty or unverifiable details
