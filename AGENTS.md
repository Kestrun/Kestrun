# AGENTS.md

This repository uses GitHub Copilot custom instructions as the main source of coding conventions.

## Always load these instructions

- Read `.github/copilot-instructions.md` if present.
- Read the relevant files in `.github/instructions/` based on the task.

## Task routing

- C# changes: `.github/instructions/csharp.instructions.md`
- PowerShell module changes: `.github/instructions/powershell-module.instructions.md`
- Pester tests: `.github/instructions/pester.instructions.md`
- OpenAPI work: `.github/instructions/openapi.instructions.md`
- Docs work: `.github/instructions/docs.instructions.md`
- Razor/UI work: `.github/instructions/razor.instructions.md`
- Tutorial/sample work: `.github/instructions/tutorial-sample.instructions.md`
- GitHub workflow and collaboration tasks: `.github/instructions/github-collaboration.instructions.md`

## Working rules

- Reuse existing abstractions instead of duplicating logic.
- Keep changes focused.
- Add or update tests for behavioral changes.
- Update docs for user-visible changes.
- Prefer the smallest viable design that matches the current architecture.

## Validation

- Run relevant build, lint, and test steps for the area you changed.
- Summarize what changed, how it was verified, and any follow-up work.
