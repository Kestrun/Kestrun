---
description: "Use when editing C# runtime code under src/CSharp. Covers placement, API shape, complexity limits, async patterns, and required test updates for Kestrun."
applyTo: "src/CSharp/**"
---

# Kestrun C# Implementation Rules

Use this file for C# changes in the runtime, tooling, and shared library projects under src/CSharp.

## Scope And Placement

- Keep production runtime code in the correct area under src/CSharp/Kestrun based on feature concern.
- Prefer extending existing namespaces and folders instead of introducing parallel abstractions.
- Keep PowerShell-surface behavior in C# core first, then expose it from the PowerShell module.
- For tool-only changes in src/CSharp/Kestrun.Tool, avoid touching core library code unless required.

## Build And Validation

- For core C# library changes, run the canonical workflow:
  - Invoke-Build Restore ; Invoke-Build Build
  - Invoke-Build Test
- Do not replace core-library validation with dotnet build. Build triggers DLL sync needed by the PowerShell module.
- For Kestrun.Tool-only changes, prefer Invoke-Build Build-KestrunTool for faster iteration.
- Use dotnet test for focused C#-only validation when PowerShell assets are not involved.

## API And Design Conventions

- Keep method cyclomatic complexity at 15 or less.
- If refactoring introduces helper methods, add XML documentation to each new helper.
- Favor small, focused methods with guard clauses and early returns.
- Preserve public API signatures unless a breaking change is explicitly requested.
- Use clear, domain-aligned names matching existing Kestrun terminology.
- Keep async code cancellation-aware and avoid blocking waits in library code.
- Align response behavior with Kestrun response helpers and documented HTTP semantics.

## Implementation Boundaries

- Runtime behavior belongs in C# core; PowerShell wrappers should stay thin.
- Avoid leaking tutorial-only logic or sample shortcuts into production runtime code.
- Reuse existing extension points, middleware patterns, and route-registration conventions.
- Keep behavior consistent across all framework targets currently targeted by the repository.

## Testing Expectations

- Add or update xUnit tests for new behavior in tests/CSharp.Tests.
- Keep regression tests narrow and tied to observable behavior.
- When behavior is surfaced to PowerShell users, add or update corresponding Pester coverage.
- Prefer the smallest relevant test scope first, then expand only as needed.

## Documentation And References

- For contribution workflow, branch naming, and PR checklist, follow [CONTRIBUTING.md](../../CONTRIBUTING.md).
- For build task inventory and orchestration details, use [Kestrun.build.ps1](../../Kestrun.build.ps1).
- For architecture and cross-language patterns, follow [.github/copilot-instructions.md](../copilot-instructions.md).
