---
applyTo: "tests/PowerShell.Tests/**/*.ps1"
---

# Pester Test Instructions

PowerShell test changes must follow `.github/copilot-instructions.md` as the global source of truth.
These rules apply when creating or editing Pester tests under `tests/PowerShell.Tests/`.

## Core Standards

- Use Pester v5 style test structure with `Describe`, `Context`, `BeforeAll`, `AfterAll`, `BeforeEach`, `AfterEach`, and `It`.
- Do not invent APIs, cmdlets, routes, defaults, or runtime behavior in tests. Verify expectations from code, examples, or existing documented behavior.
- Prefer small, focused test changes that cover the requested behavior without broad refactors.
- Keep tests deterministic, isolated, and safe to run repeatedly in CI and local development.
- Name blocks and assertions descriptively so failures explain the behavior that regressed.
- Prefer adding or updating assertions for the changed behavior instead of rewriting unrelated tests.

## Verification Workflow

1. Read the target test file before editing.
2. Inspect the related PowerShell module code, C# implementation, tutorial script, or existing helper used by the test.
3. Update only the expectations and setup required for the requested behavior.
4. Run the narrowest relevant Pester scope when practical.
5. If behavior cannot be verified from source, omit the assertion or mark the uncertainty clearly.

## Repository-Specific Guidance

- Keep PowerShell tests under `tests/PowerShell.Tests/`, matching existing naming such as `FeatureName.Tests.ps1`.
- For focused local runs, prefer `Invoke-Pester` directly over editor-integrated test runners.
- When a test exercises tutorial or example scripts, dot-source `tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1` and reuse shared helpers such as:
  - `Start-ExampleScript`
  - `Stop-ExampleScript`
  - `Write-KrExampleInstanceOnFailure`
  - `Test-ExampleRouteSet`
  - `New-TestFile`
- For tutorial/example tests, clean up started processes in `AfterAll` and emit failure diagnostics with `Write-KrExampleInstanceOnFailure`.
- Prefer readiness helpers and polling already provided by the harness instead of adding arbitrary sleeps.
- Keep multipart and upload test files small for CI speed, even when the production scenario represents larger limits.
- Use tags such as `Tutorial`, `Integration`, `Slow`, or feature-specific tags only when they reflect how the suite is already organized.

## Assertion Guidance

- Assert observable behavior: status codes, headers, response bodies, thrown errors, generated files, or returned objects.
- Prefer exact assertions when the contract is stable; use bounded or shape-based assertions only when the implementation intentionally allows variation.
- Keep each `It` block focused on one behavior or one closely related expectation set.
- When asserting HTTP responses, account for the response object shape used by the current helper or PowerShell API, as existing tests do.
- Avoid overly brittle assertions on formatting, incidental ordering, timestamps, random values, or full exception text unless that output is the contract.

## Setup And Cleanup

- Use `Set-StrictMode` and fail-fast patterns only when they match the surrounding test file or shared helper conventions.
- Scope shared runtime state carefully, typically with `$script:` variables inside a `Describe` block when the suite already follows that pattern.
- Always dispose of temporary files, processes, certificates, sockets, or other external resources created by the test.
- Do not leave background servers or modified machine state behind after the test run.
- If a test depends on platform-specific or external prerequisites, gate it with existing skip patterns rather than allowing flaky failures.

## Change Discipline

- Do not modify production code while working on a test-only request unless explicitly asked.
- If the requested behavior implies a product bug, keep the test change minimal and note the gap rather than masking the failure.
- Preserve existing test organization and helper usage unless there is a clear maintenance reason to change it.

## Completion Notes

When reporting Pester test work, explain:

- what tests were added or updated
- what repository sources were used to verify the expectations
- what test scope was run, if any
- any remaining uncertainty, skips, or environment-specific limitations
