---
description: "Use when shipping a PowerShell cmdlet change in Kestrun with module-aware build and Pester validation."
name: "Ship PowerShell Cmdlet Change"
argument-hint: "Describe the cmdlet change and expected user-facing behavior."
agent: "agent"
---

Implement the requested PowerShell cmdlet change in src/PowerShell/Kestrun and validate with Kestrun module conventions.

## Requirements

1. Read workspace instructions relevant to PowerShell module and tests before editing.
2. Keep cmdlet behavior pipeline-friendly and preserve compatibility unless the request requires a contract change.
3. Add or update comment-based help for public cmdlet changes.
4. Make minimal, reviewable edits.
5. Validate with canonical module-aware flow from repository root:
   - Invoke-Build Restore ; Invoke-Build Build
   - Invoke-Build Test
6. While iterating, run focused Pester where practical, then confirm with broader validation.
7. Add or update tests in tests/PowerShell.Tests for changed behavior.
8. If runtime behavior changes are required, implement in C# core first and keep wrapper semantics aligned.
9. Do not revert unrelated local changes.

## Output Format

- Summary: what changed and why.
- Cmdlet Contract: parameters, behavior, and help updates.
- Files: edited file list.
- Validation: commands run and pass or fail outcomes.
- Risks or Follow-ups: only if relevant.
