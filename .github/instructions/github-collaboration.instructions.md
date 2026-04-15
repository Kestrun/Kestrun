---
description: "Use when creating, updating, or triaging GitHub issues and pull requests in Kestrun. Covers issue quality, branch readiness, PR creation flow, and reviewer-ready descriptions."
applyTo: ".github/**"
---

# GitHub Issue And PR Workflow

Use this instruction when a task involves opening an issue, creating a pull request, or preparing content for either.

## Opening Or Updating Issues

- Verify whether an issue already exists before creating a new one.
- Use a clear, specific title describing the problem or feature.
- Include:
  - Problem statement
  - Expected behavior
  - Actual behavior (for bugs)
  - Reproduction steps (for bugs)
  - Scope and acceptance criteria (for features)
- Add environment details when relevant (PowerShell version, .NET SDK, OS).
- Link to related files, tests, or docs paths when they help maintainers reproduce or review.
- Apply labels, assignees, and milestone only when confidence is high.

## Issue Writing Quality

- Prefer concise technical language over broad narrative.
- Include minimal examples that reproduce behavior.
- Avoid speculative root-cause claims unless supported by code or tests.
- If unknown, explicitly state unknown instead of guessing.

## PR Readiness Before Opening

- Confirm there are committed changes intended for review.
- Check branch status (ahead/behind, upstream configured).
- Do not include unrelated local edits.
- Run expected local validation for the change scope:
  - Canonical: Invoke-Build Restore ; Invoke-Build Build
  - Canonical tests: Invoke-Build Test
  - Focused C# iteration: dotnet test
  - Tool-only iteration: Invoke-Build Build-KestrunTool

## Creating A Pull Request

- Use a branch that follows repository naming expectations from CONTRIBUTING.md.
- Use an imperative PR title, concise and specific.
- In PR body, include:
  - Summary of what changed
  - Why the change was needed
  - Validation commands and outcomes
  - Any docs/test updates
  - Issue linkage (Fixes #NN or Refs #NN when applicable)
- Create as draft only if work is not ready for full review.

## Reviewer-Friendly PR Content

- Call out any risk areas or behavior changes clearly.
- Mention breaking changes explicitly.
- Keep the diff minimal and scoped.
- If follow-up work is deferred, list it under a short follow-up section.

## Repository References

- Contribution and naming conventions: [CONTRIBUTING.md](../../CONTRIBUTING.md)
- Global agent workflow guidance: [.github/copilot-instructions.md](../copilot-instructions.md)
- PR template: [.github/pull_request_template.md](../pull_request_template.md)
