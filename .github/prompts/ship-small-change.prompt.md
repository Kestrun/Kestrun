---
description: "Ship a small Kestrun change with the canonical restore/build/test flow and a concise, review-ready report."
name: "Ship Small Change"
argument-hint: "Describe the change to make (file/scope + expected behavior)."
agent: "agent"
---

Implement the requested small change in this repository and finish with a concise validation report.

## Requirements

1. Read relevant workspace instructions and existing patterns before editing.
2. Make the smallest safe code change that satisfies the request.
3. Validate with the canonical local flow from repository root:

```powershell
Invoke-Build Restore ; Invoke-Build Build
Invoke-Build Test
```

4. If the change is strictly scoped to src/CSharp/Kestrun.Tool, use this faster build step while iterating:

```powershell
Invoke-Build Build-KestrunTool
```

5. Add or update targeted tests when behavior changes.
6. Do not revert unrelated local modifications.

## Output Format

Provide a short report with these sections:

- Summary: what changed and why.
- Files: list edited files.
- Validation: commands run and pass/fail outcomes.
- Risks or Follow-ups: only if relevant.

Keep the report brief and PR-ready.
